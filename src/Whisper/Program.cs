using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

var options = Options.Parse(args);

if (args.Length == 0 && Ui.Interactive)
{
    var action = await Dashboard.RunAsync();
    switch (action)
    {
        case DashboardAction.Transcribe:
        {
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Audio file path[/]")
                    .PromptStyle("white")
                    .Validate(path => File.Exists(path) ? ValidationResult.Success() : ValidationResult.Error("[red]File not found.[/]")));
            var output = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Output path[/] [grey](optional, .txt or .srt)[/]")
                    .AllowEmpty()
                    .PromptStyle("white"));
            args = string.IsNullOrWhiteSpace(output) ? [input] : [input, output];
            break;
        }
        case DashboardAction.Microphone:
        {
            var output = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Transcript path[/] [grey](optional .txt file)[/]")
                    .AllowEmpty()
                    .PromptStyle("white"));
            args = string.IsNullOrWhiteSpace(output) ? ["--mic"] : ["--mic", output];
            break;
        }
        case DashboardAction.Download:
            args = ["--download"];
            break;
        case DashboardAction.Uninstall:
            args = ["--uninstall"];
            break;
        default:
            return 0;
    }

    options = Options.Parse(args);
}

if (options.Error is { } error)
{
    Ui.Error($"{error} Use -h for help.");
    return 2;
}

if (options.Help || args.Length == 0)
{
    Usage.Write();
    return 0;
}

if (options.Download || options.Uninstall || !Ui.Interactive) Ui.Banner();

if (options.Download)
{
    var choice = options.Level is { } level ? Models.Get(level) : await ModelPicker.ChooseForDownloadAsync();
    if (choice is not null)
    {
        Cancellation.Watch();
        try
        {
            await ModelManager.DownloadAsync(choice);
        }
        catch (OperationCanceledException)
        {
            Ui.Warn("Download stopped. Run it again to resume.");
        }
    }
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

Screen.Enter();
Ui.NewPage();
var mic = options.Mic ? MicPicker.Pick(options.MicDevice) : null;
if (options.Mic && mic is null) return 1;
var selection = ModelPicker.FindInstalled(options.Level, options.Mic);
if (selection.Cancelled) return 0;
if (selection.Model is not { } model) return 1;
Ui.NewPage();
Ui.Info($"Model: {Ui.Bold(model.Level.ToString())} {Ui.Dim(model.Note)}");
if (mic is { } picked) Ui.Info($"Microphone: {Ui.Bold(picked.Name)}");

if (options.Benchmark)
{
    Cancellation.Watch();
    return await Benchmark.RunAsync(model, options.Input!, options.Language, Cancellation.Token);
}

var index = 0;
Console.WriteLine();
Cancellation.Watch(enterClears: options.Mic);
if (!options.Mic && Ui.Interactive) Ui.Info("Press " + Ui.Bold("Esc") + " to stop.");
var stop = Cancellation.Abort;
Status.Start();
try
{
    Status.Work($"Loading {model.Level} model…");
    await using var transcriber = new Transcriber(model.LocalPath, options.Language, options.Mic, options.Device != ComputeDevice.Cpu);
    var compute = transcriber.Compute;
    Ui.Info("Compute: " + Ui.Bold($"{compute.Label} · {compute.Device}") + " " + Ui.Dim($"via {compute.Library}"));
    Ui.Info("CPU: " + Ui.Bold(compute.Cpu) + " " + Ui.Dim($"· {compute.Threads} threads"));
    if (!compute.UsesGpu && options.Device != ComputeDevice.Cpu)
    {
        Ui.Warn($"No usable GPU backend was found; running on the CPU (loaded runtime: {compute.Library}).");
        foreach (var line in compute.Log.Where(l => l.Contains("vulkan", StringComparison.OrdinalIgnoreCase) || l.Contains("gpu", StringComparison.OrdinalIgnoreCase)).Take(8))
            Ui.Info(Ui.Dim(line.Trim()));
    }
    await using var file = options.Output is { } path ? new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = true } : null;

    Status.Work(options.Mic ? null : "Decoding audio…");
    List<(byte[] Pcm, TimeSpan Start)> chunks = options.Mic
        ? []
        : Chunker.Split(AudioConverter.ToPcm(options.Input!)).ToList();
    var sources = options.Mic
        ? MicRecorder.ChunksAsync(mic!.Value.Id).Select(wav => (Audio: wav, Start: TimeSpan.Zero))
        : chunks.Select(c => (Audio: (Stream)Wav.FromPcm(c.Pcm), c.Start)).ToAsyncEnumerable();
    Status.Work(null);
    if (!options.Mic) Ui.Info($"Audio decoded: {Ui.Bold(chunks.Count.ToString())} speech chunk(s)");
    var chunk = 0;

    await foreach (var (audio, start) in sources.WithCancellation(stop))
    {
        await using (audio)
        {
            Status.Work(options.Mic
                ? $"Transcribing on {compute.Label}"
                : $"Transcribing {++chunk}/{chunks.Count} on {compute.Label}");
            using var chunkStop = CancellationTokenSource.CreateLinkedTokenSource(stop, Cancellation.Flush);
            try
            {
                await foreach (var segment in transcriber.RunAsync(audio, chunkStop.Token))
                {
                    var text = segment.Text.Trim();
                    if (text.Length == 0) continue;

                    index++;
                    Ui.Segment(options.Mic ? null : segment.Start + start, text);
                    if (file is not null) await file.WriteLineAsync(SegmentFormatter.Render(segment, index, options.Format, start));
                }
            }
            catch (OperationCanceledException) when (!stop.IsCancellationRequested)
            {
                // Enter cleared the live buffer; carry on with the next phrase.
            }

            Status.Work(null);
        }
    }
}
catch (OperationCanceledException)
{
    Ui.Warn("Stopped.");
}
finally
{
    Status.Stop();
}

void Summary()
{
    Console.WriteLine();
    Ui.Info($"Done: {Ui.Bold(index.ToString())} segment(s)");
    if (options.Output is { } saved) Ui.Info($"Saved to {Ui.Bold(saved)}");
}

if (Screen.Active && !options.Mic && !Cancellation.Abort.IsCancellationRequested)
{
    Summary();
    Cancellation.Finish();
    Ui.Info("Press " + Ui.Bold("Enter") + ", " + Ui.Bold("Esc") + " or " + Ui.Bold("Ctrl+X") + " to exit.");
    try
    {
        await Task.Delay(Timeout.Infinite, Cancellation.Abort);
    }
    catch (OperationCanceledException)
    {
    }
}

var fullScreen = Screen.Active;
Screen.Leave();
if (fullScreen)
    Ui.Recap($"Done: {Ui.Bold(index.ToString())} segment(s)" + (options.Output is { } savedTo ? $" · saved to {Ui.Bold(savedTo)}" : ""));
else
    Summary();
return 0;
