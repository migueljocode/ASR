Console.OutputEncoding = Encoding.UTF8;

var options = Options.Parse(args);

if (options.Error is { } error)
{
    Ui.Error($"{error} Use -h for help.");
    return 2;
}

if (options.Help || args.Length == 0)
{
    Console.WriteLine(Usage.Text);
    return 0;
}

Ui.Banner();

if (options.Download)
{
    var choice = options.Level is { } level ? Models.Get(level) : await ModelPicker.ChooseForDownloadAsync();
    if (choice is not null) await ModelManager.DownloadAsync(choice);
    return 0;
}

if (options.Uninstall)
{
    var target = options.Level is { } level ? Models.Get(level) : ModelPicker.ChooseForUninstall();
    if (target is not null) ModelManager.Uninstall(target);
    return 0;
}

if (!options.Mic && !File.Exists(options.Input))
{
    Ui.Error($"Input file not found: {options.Input}. Use -h for help.");
    return 1;
}

if (options is { Mic: true, Format: Format.Srt })
{
    Ui.Error("--mic supports .txt output only.");
    return 1;
}

var device = options.Mic ? MicPicker.Pick() : string.Empty;
if (ModelPicker.FindInstalled(options.Level, options.Mic) is not { } model) return 1;

var index = 0;
Console.WriteLine();
Status.Start();
try
{
    Status.Work("Loading model…");
    await using var transcriber = new Transcriber(model.LocalPath, options.Language, options.Mic);
    await using var file = options.Output is { } path ? new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = true } : null;

    Status.Work(options.Mic ? null : "Decoding audio…");
    var sources = options.Mic
        ? MicRecorder.ChunksAsync(device).Select(wav => (Audio: wav, Start: TimeSpan.Zero))
        : Chunker.Split(AudioConverter.ToPcm(options.Input!))
            .Select(c => (Audio: (Stream)Wav.FromPcm(c.Pcm), c.Start))
            .ToAsyncEnumerable();
    Status.Work(null);

    await foreach (var (audio, start) in sources)
    {
        await using (audio)
        {
            Status.Work("Transcribing…");
            await foreach (var segment in transcriber.RunAsync(audio))
            {
                var text = segment.Text.Trim();
                if (text.Length == 0) continue;

                index++;
                Ui.Segment(options.Mic ? null : segment.Start + start, text);
                if (file is not null) await file.WriteLineAsync(SegmentFormatter.Render(segment, index, options.Format, start));
            }

            Status.Work(null);
        }
    }
}
finally
{
    Status.Stop();
}

Console.WriteLine();
Ui.Info($"Done: {Ui.Bold(index.ToString())} segment(s)");
if (options.Output is { } saved) Ui.Info($"Saved to {Ui.Bold(saved)}");
return 0;
