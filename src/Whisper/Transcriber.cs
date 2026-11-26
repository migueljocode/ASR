sealed class Transcriber : IAsyncDisposable
{
    readonly WhisperFactory _factory;
    readonly WhisperProcessor _processor;

    public Transcriber(string modelPath, string language, bool live)
    {
        _factory = WhisperFactory.FromPath(modelPath);
        var builder = _factory.CreateBuilder()
            .WithLanguage(language)
            .WithNoContext()
            .WithThreads(Math.Clamp(Environment.ProcessorCount, 1, 8));
        if (live) builder = builder.WithSingleSegment();
        _processor = builder.Build();
    }

    public IAsyncEnumerable<SegmentData> RunAsync(Stream wav) => _processor.ProcessAsync(wav);

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
        _factory.Dispose();
    }
}
