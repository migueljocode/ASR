sealed class Transcriber : IAsyncDisposable
{
    readonly WhisperFactory _factory;
    readonly WhisperProcessor _processor;
    readonly Dictionary<string, WhisperProcessor> _restricted = new(StringComparer.OrdinalIgnoreCase);
    static readonly int Threads = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);

    public ComputeInfo Compute { get; }

    public Transcriber(string modelPath, string language, bool live, bool useGpu)
    {
        RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];
        List<string> log = [];
        using (LogProvider.AddLogger((_, message) =>
        {
            if (message is not null) lock (log) log.Add(message);
        }))
            _factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = useGpu });
        Compute = Hardware.Describe(RuntimeOptions.LoadedLibrary, log, Threads, useGpu);
        var languages = Options.SplitLanguages(language);
        _processor = Build(languages.Length == 1 ? languages[0] : "auto");
        if (languages.Length > 1)
            foreach (var code in languages) _restricted[code] = Build(code);

        WhisperProcessor Build(string code)
        {
            var builder = _factory.CreateBuilder().WithLanguage(code).WithNoContext().WithThreads(Threads);
            return (live ? builder.WithSingleSegment() : builder).Build();
        }
    }

    public IAsyncEnumerable<SegmentData> RunAsync(Stream wav, CancellationToken ct = default) =>
        _restricted.Count == 0 ? _processor.ProcessAsync(wav, ct) : RunRestrictedAsync(wav, ct);

    // Detects the language first; if it is outside the allowed set, re-runs the chunk in each allowed language and keeps the most confident result.
    async IAsyncEnumerable<SegmentData> RunRestrictedAsync(Stream wav, [EnumeratorCancellation] CancellationToken ct)
    {
        var segments = await CollectAsync(_processor, wav, ct);
        if (segments.Any(s => s.Language is not { } code || !_restricted.ContainsKey(code)))
        {
            List<List<SegmentData>> candidates = [];
            foreach (var processor in _restricted.Values) candidates.Add(await CollectAsync(processor, wav, ct));
            segments = candidates.MaxBy(c => c.Count == 0 ? 0 : c.Average(s => s.Probability))!;
        }

        foreach (var segment in segments) yield return segment;
    }

    static async Task<List<SegmentData>> CollectAsync(WhisperProcessor processor, Stream wav, CancellationToken ct)
    {
        if (wav.CanSeek) wav.Position = 0;
        List<SegmentData> segments = [];
        await foreach (var segment in processor.ProcessAsync(wav, ct)) segments.Add(segment);
        return segments;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _restricted.Values) await processor.DisposeAsync();
        await _processor.DisposeAsync();
        _factory.Dispose();
    }
}
