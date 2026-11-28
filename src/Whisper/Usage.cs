using Spectre.Console;

static class Usage
{
    public static void Write()
    {
        var commands = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Command[/]").NoWrap())
            .AddColumn("[bold]What it does[/]");

        commands.AddRow("[cyan]Whisper <input> [[output]][/]", "Transcribe audio; output format is selected by the file extension.");
        commands.AddRow("[cyan]Whisper --mic [[output]][/]", "Transcribe from the default microphone. Enter clears the buffer, Esc stops.");
        commands.AddRow("[cyan]Whisper --download [[model]][/]", "Download a model, with an interactive picker when omitted.");
        commands.AddRow("[cyan]Whisper --uninstall [[model]][/]", "Remove a model, with an interactive picker when omitted.");
        commands.AddRow("[cyan]Whisper --benchmark <input>[/]", "Time the same audio on the CPU and the GPU and compare.");

        var options = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("[bold]Option[/]").NoWrap())
            .AddColumn("[grey]Description[/]");

        options.AddRow("[cyan]-i, --input <path>[/]", "Specify the input file by option instead of positionally.");
        options.AddRow("[cyan]-m, -p, --model <name>[/]", "Prefer tiny, base, small, medium, turbo, or large.");
        options.AddRow("[cyan]-l, --language <code>[/]", "Recognition language; defaults to auto (for example: en, fa, tr, or fa+en to allow only those).");
        options.AddRow("[cyan]-d, --device <name>[/]", "Run on auto (GPU when available), cpu, or gpu.");
        options.AddRow("[cyan]--mic-device <number>[/]", "Microphone number for --mic; asks when omitted and several exist.");
        options.AddRow("[cyan]-h, --help[/]", "Show this help.");
        options.AddRow("[grey][[output]][/]", "Optional .txt or .srt path; omit to print transcription only.");

        var examples = new Markup(
            "[grey]Whisper[/] [white]talk.mp3 talk.txt[/]\n" +
            "[grey]Whisper[/] [white]talk.mp3 subtitles.srt -p medium -l fa[/]\n" +
            "[grey]Whisper[/] [white]--mic notes.txt -l en[/]\n" +
            "[grey]Whisper[/] [white]talk.mp3 talk.txt -l fa+en[/]\n" +
            "[grey]Whisper[/] [white]--mic notes.txt -l fa+en[/]\n" +
            "[grey]Whisper[/] [white]--download turbo[/]");

        AnsiConsole.Write(new Panel(
                new Rows(
                    new Markup("[bold]Local speech recognition, right in your terminal.[/]"),
                    new Text(""),
                    commands,
                    new Text(""),
                    new Markup("[bold]Options[/]"),
                    options,
                    new Text(""),
                    new Markup("[bold]Examples[/]"),
                    examples))
            .Header("[bold #c4b5fd] Whisper [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(1, 0));
    }
}
