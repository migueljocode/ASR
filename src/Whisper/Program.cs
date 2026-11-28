using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

var options = Options.Parse(args);

if (args.Length == 0 && Ui.Interactive)
{
    var action = await Dashboard.RunAsync();
    if (action != DashboardAction.Exit)
    {
        Screen.Enter();
        Ui.NewPage();
    }

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
    Screen.Enter();
    Usage.Write();
    await Screen.HoldAsync();
    Screen.Leave();
    return 0;
}

if (options.MicDevice == 0)
{
    Screen.Enter();
    Ui.NewPage();
    MicPicker.Show();
    await Screen.HoldAsync();
    Screen.Leave();
    return 0;
}

if (options.Download || options.Uninstall) Screen.Enter();
if (Screen.Active) Ui.NewPage();
else if (options.Download || options.Uninstall || !Ui.Interactive) Ui.Banner();

if (options.Download)
{
    var choice = options.Level is { } level ? Models.Get(level) : await ModelPicker.ChooseForDownloadAsync();
    string? recap = null;
    if (choice is not null)
    {
        Cancellation.Watch();
        try
        {
            await ModelManager.DownloadAsync(choice);
            recap = $"Model {Ui.Bold(choice.Level.ToString())} is ready";
        }
        catch (OperationCanceledException)
        {
            Ui.Warn("Download stopped. Run it again to resume.");
            recap = "Download stopped. Run it again to resume.";
        }
    }

    var downloadScreen = Screen.Active;
    Screen.Leave();
    if (downloadScreen && recap is not null) Ui.Recap(recap);
    return 0;
}

if (options.Uninstall)
{
    var target = options.Level is { } level ? Models.Get(level) : ModelPicker.ChooseForUninstall();
    var removed = target is not null && ModelManager.Uninstall(target);
    var uninstallScreen = Screen.Active;
    Screen.Leave();
    if (uninstallScreen) Ui.Recap(removed ? $"Removed {Ui.Bold(target!.Level.ToString())}" : "No model was removed.");
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
var (mic, micCancelled) = options.Mic ? MicPicker.Pick(options.MicDevice) : (null, false);
if (micCancelled) return 0;
if (options.Mic && mic is null) return 1;
var selection = ModelPicker.FindInstalled(options.Level, options.Mic);
if (selection.Cancelled) return 0;
if (selection.Model is not { } model) return 1;
Ui.NewPage();
var languageLabel = options.Language.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "all" : string.Join("+", Options.SplitLanguages(options.Language));
List<(string, string)> session =
[
    ("Model", $"{Ui.Bold(model.Level.ToString())} {Ui.Dim(model.Note)}"),
    ("Language", Ui.Escape(languageLabel == "all" ? "all (auto-detect)" : languageLabel))
];
if (mic is { } picked) session.Add(("Microphone", Ui.Escape(picked.Name)));
if (options.Input is not null) session.Add(("Input", Ui.Escape(options.Input)));
if (options.Output is not null) session.Add(("Output", Ui.Escape(options.Output)));
Ui.Card("SESSION", session);

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
    Ui.Card("COMPUTE",
    [
        ("Device", Ui.Escape($"{compute.Label} · {compute.Device}")),
        ("Runtime", Ui.Escape(compute.Library)),
        ("CPU", Ui.Escape($"{compute.Cpu} · {compute.Threads} threads"))
    ]);
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
                ? $"Transcribing {languageLabel} on {compute.Label}"
                : $"Transcribing {++chunk}/{chunks.Count} · {languageLabel} on {compute.Label}");
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
    List<(string, string)> rows = [("Segments", index.ToString())];
    if (options.Output is not null) rows.Add(("Saved to", Ui.Escape(options.Output)));
    rows.Add(("Status", Ui.Green("All done.")));
    Ui.Card("SUMMARY", rows);
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
