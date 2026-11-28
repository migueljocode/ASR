using Spectre.Console;

static class Benchmark
{
    sealed record Result(ComputeInfo Compute, TimeSpan Load, TimeSpan Transcribe);

    public static async Task<int> RunAsync(ModelInfo model, string input, string language, CancellationToken ct)
    {
        var chunks = Chunker.Split(AudioConverter.ToPcm(input)).Select(c => c.Pcm).ToList();
        if (chunks.Count == 0)
        {
            Ui.Error("No speech was found in the audio.");
            return 1;
        }

        var audio = TimeSpan.FromSeconds(chunks.Sum(c => c.Length) / (double)(Wav.Rate * 2));
        Ui.Info($"Benchmark: {Ui.Bold(model.Level.ToString())} model, {Ui.Bold(audio.TotalSeconds.ToString("F1"))} s of speech in {chunks.Count} chunk(s)");

        List<Result> results = [];
        Status.Start();
        try
        {
            foreach (var useGpu in new[] { false, true })
                if (await MeasureAsync(model, language, chunks, useGpu, ct) is { } result) results.Add(result);
        }
        catch (OperationCanceledException)
        {
            Ui.Warn("Benchmark stopped.");
            return 0;
        }
        finally
        {
            Status.Stop();
        }

        Report(results, audio);
        return 0;
    }

    // One untimed warm-up chunk first, so GPU shader compilation does not skew the result.
    static async Task<Result?> MeasureAsync(ModelInfo model, string language, List<byte[]> chunks, bool useGpu, CancellationToken ct)
    {
        var label = useGpu ? "GPU" : "CPU";
        Status.Work($"Loading model for {label}…");
        var clock = Stopwatch.StartNew();
        await using var transcriber = new Transcriber(model.LocalPath, language, false, useGpu);
        var load = clock.Elapsed;

        if (useGpu && !transcriber.Compute.UsesGpu)
        {
            Ui.Warn("GPU backend unavailable; skipping the GPU run.");
            return null;
        }

        Status.Work($"Warming up {label}…");
        await TranscribeAsync(transcriber, chunks.Take(1), ct);
        Status.Work($"Benchmarking {label}…");
        return new(transcriber.Compute, load, await TranscribeAsync(transcriber, chunks, ct));
    }

    static async Task<TimeSpan> TranscribeAsync(Transcriber transcriber, IEnumerable<byte[]> chunks, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        foreach (var chunk in chunks)
        {
            await using var wav = Wav.FromPcm(chunk);
            await foreach (var _ in transcriber.RunAsync(wav, ct)) { }
        }

        return clock.Elapsed;
    }

    static void Report(List<Result> results, TimeSpan audio)
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
            .AddColumn("[bold]Device[/]")
            .AddColumn("[bold]Hardware[/]")
            .AddColumn(new TableColumn("[bold]Model load[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Transcribe[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Speed[/]").RightAligned());

        foreach (var r in results)
            table.AddRow(
                $"[bold]{Ui.Escape(r.Compute.Label)}[/]",
                Ui.Escape(r.Compute.Device),
                $"{r.Load.TotalSeconds:F1} s",
                $"{r.Transcribe.TotalSeconds:F1} s",
                $"{audio / r.Transcribe:F2}× real-time");

        Console.WriteLine();
        AnsiConsole.Write(table);

        if (results is not [var cpu, var gpu]) return;
        var ratio = cpu.Transcribe / gpu.Transcribe;
        AnsiConsole.MarkupLine(ratio >= 1
            ? $"{Ui.Indent}[green]GPU is {ratio:F2}× faster than CPU[/]"
            : $"{Ui.Indent}[yellow]CPU is {1 / ratio:F2}× faster than GPU[/]");
    }
}
