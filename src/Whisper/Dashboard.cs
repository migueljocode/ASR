using Spectre.Console;
using Spectre.Console.Rendering;

enum DashboardAction { Exit, Transcribe, Microphone, Download, Uninstall }
enum DashboardPage { Home, Models, Help }

static class Dashboard
{
    sealed record Choice(string Title, string Detail, DashboardAction? Action, DashboardPage? Page);
    sealed record PageTheme(string Background, int Red, int Green, int Blue, string Accent, string Card, string Selected);
    [StructLayout(LayoutKind.Sequential)]
    struct PollDescriptor
    {
        public int FileDescriptor;
        public short Events;
        public short ReturnedEvents;
    }

    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    static extern int Poll(ref PollDescriptor descriptors, uint count, int timeout);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    static extern nint ReadTerminalByte(int fileDescriptor, out byte value, nuint count);
    static readonly Choice[] HomeChoices =
    [
        new("Transcribe audio", "Convert a recording into text or subtitles.", DashboardAction.Transcribe, null),
        new("Live microphone", "Capture speech and transcribe it as you speak.", DashboardAction.Microphone, null),
        new("Model library", "Browse, download, or remove speech models.", null, DashboardPage.Models),
        new("Quick guide", "Commands, shortcuts, and practical tips.", null, DashboardPage.Help)
    ];

    static readonly Choice[] ModelChoices =
    [
        new("Download a model", "Choose from six local recognition models.", DashboardAction.Download, null),
        new("Remove a model", "Free disk space by removing a downloaded model.", DashboardAction.Uninstall, null),
        new("Back to home", "Return to the main menu.", null, DashboardPage.Home)
    ];

    static readonly string[] Spinner = ["◐", "◓", "◑", "◒"];
    static DashboardPage _page = DashboardPage.Home;
    static int _selection;
    static int _frame;

    public static async Task<DashboardAction> RunAsync()
    {
        var oldControlC = Console.TreatControlCAsInput;
        var savedTerminalMode = OperatingSystem.IsWindows() ? null : RunStty("-g");
        var alternateScreen = false;
        DashboardAction result = DashboardAction.Exit;

        try
        {
            Console.TreatControlCAsInput = true;
            if (savedTerminalMode is not null)
                RunStty("-echo", "-icanon", "-isig", "min", "1", "time", "0");

            Console.Write("\e[?1049h\e[?25l");
            alternateScreen = true;
            SetTheme();
            await AnsiConsole.Live(Render())
                .AutoClear(false)
                .StartAsync(async context =>
                {
                    var animationClock = Stopwatch.StartNew();
                    while (true)
                    {
                        await Task.Delay(20);
                        if (ReadAvailableKey() is { } key)
                        {
                            var previousPage = _page;
                            var action = HandleKey(key);
                            if (action is { } selected)
                            {
                                result = selected;
                                break;
                            }
                            if (_page != previousPage) SetTheme();
                            context.UpdateTarget(Render());
                            animationClock.Restart();
                            continue;
                        }

                        if (animationClock.Elapsed < TimeSpan.FromMilliseconds(90)) continue;
                        _frame++;
                        context.UpdateTarget(Render());
                        animationClock.Restart();
                    }
                });
        }
        finally
        {
            try
            {
                Console.TreatControlCAsInput = oldControlC;
                if (savedTerminalMode is not null) RunStty(savedTerminalMode);
            }
            finally
            {
                if (alternateScreen) Console.Write("\e]111\a\e[0m\e[?25h\e[?1049l");
                Console.Out.Flush();
            }
        }

        return result;
    }

    static DashboardAction? HandleKey(ConsoleKeyInfo key)
    {
        if (key.Key is ConsoleKey.Q or ConsoleKey.Escape ||
            key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            return DashboardAction.Exit;

        if (key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1)
            SetPage(DashboardPage.Home);
        else if (key.Key is ConsoleKey.D2 or ConsoleKey.NumPad2)
            SetPage(DashboardPage.Models);
        else if (key.Key is ConsoleKey.D3 or ConsoleKey.NumPad3)
            SetPage(DashboardPage.Help);
        else if (key.Key == ConsoleKey.Tab)
            SetPage((DashboardPage)(((int)_page + 1) % 3));
        else if (key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.LeftArrow or ConsoleKey.RightArrow)
            MoveSelection(key.Key);
        else if (key.Key == ConsoleKey.Enter)
        {
            var choices = CurrentChoices();
            if (choices.Length == 0) return null;
            var choice = choices[_selection];
            if (choice.Page is { } page)
                SetPage(page);
            else
                return choice.Action;
        }

        return null;
    }

    static void SetPage(DashboardPage page)
    {
        _page = page;
        _selection = 0;
    }

    static void MoveSelection(ConsoleKey key)
    {
        var count = CurrentChoices().Length;
        if (count == 0) return;

        if (_page == DashboardPage.Home)
        {
            switch (key)
            {
                case ConsoleKey.LeftArrow when _selection % 2 == 1:
                    _selection--;
                    break;
                case ConsoleKey.RightArrow when _selection % 2 == 0 && _selection + 1 < count:
                    _selection++;
                    break;
                case ConsoleKey.UpArrow or ConsoleKey.DownArrow:
                    _selection = (_selection + 2) % count;
                    break;
            }
            return;
        }

        var direction = key is ConsoleKey.UpArrow or ConsoleKey.LeftArrow ? -1 : 1;
        _selection = (_selection + direction + count) % count;
    }

    static Choice[] CurrentChoices() => _page switch
    {
        DashboardPage.Home => HomeChoices,
        DashboardPage.Models => ModelChoices,
        _ => []
    };

    static void SetTheme()
    {
        var theme = CurrentTheme();
        Console.Write($"\e]11;{theme.Background}\a\e[48;2;{theme.Red};{theme.Green};{theme.Blue}m\e[2J\e[H");
        Console.Out.Flush();
    }

    static ConsoleKeyInfo ReadKey()
    {
        var key = ReadInputByte();
        if (key < 0) return new('\0', ConsoleKey.NoName, false, false, false);

        if (key == 0x1b)
        {
            var prefix = ReadAvailableByte(35);
            if (prefix is '[' or 'O')
            {
                var arrow = ReadAvailableByte(35);
                var arrowKey = arrow switch
                {
                    'A' => ConsoleKey.UpArrow,
                    'B' => ConsoleKey.DownArrow,
                    'C' => ConsoleKey.RightArrow,
                    'D' => ConsoleKey.LeftArrow,
                    _ => ConsoleKey.NoName
                };
                if (arrowKey != ConsoleKey.NoName)
                    return new('\0', arrowKey, false, false, false);
            }

            return new('\0', ConsoleKey.Escape, false, false, false);
        }

        if (key is 0x03)
            return new('c', ConsoleKey.C, false, false, true);
        if (key is '\r' or '\n')
            return new('\r', ConsoleKey.Enter, false, false, false);
        if (key == '\t')
            return new('\t', ConsoleKey.Tab, false, false, false);

        var character = (char)key;
        var name = char.IsLetter(character) ? char.ToUpperInvariant(character).ToString()
            : char.IsDigit(character) ? $"D{character}"
            : string.Empty;
        var consoleKey = Enum.TryParse<ConsoleKey>(name, out var parsed) ? parsed : ConsoleKey.NoName;
        return new(character, consoleKey, false, false, false);
    }

    static ConsoleKeyInfo? ReadAvailableKey()
    {
        if (OperatingSystem.IsWindows())
            return Console.KeyAvailable ? Console.ReadKey(true) : null;

        var descriptor = new PollDescriptor { FileDescriptor = 0, Events = 1 };
        return Poll(ref descriptor, 1, 0) > 0 ? ReadKey() : null;
    }

    static int ReadAvailableByte(int timeoutMilliseconds)
    {
        var descriptor = new PollDescriptor { FileDescriptor = 0, Events = 1 };
        if (Poll(ref descriptor, 1, timeoutMilliseconds) <= 0) return -1;
        return ReadInputByte();
    }

    static int ReadInputByte() =>
        ReadTerminalByte(0, out var value, 1) == 1 ? value : -1;

    static string RunStty(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("stty")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start stty to configure terminal input.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Could not configure terminal input: {error.Trim()}");
        return output.Trim();
    }

    static PageTheme CurrentTheme() => _page switch
    {
        DashboardPage.Models => new("#0F2A33", 15, 42, 51, "#5FE0D0", "#17404A", _frame % 8 < 4 ? "#1F6B6E" : "#1A5A5E"),
        DashboardPage.Help => new("#2A1D33", 42, 29, 51, "#F0A8FF", "#3A2845", _frame % 8 < 4 ? "#6A4575" : "#58395F"),
        _ => new("#1A1530", 26, 21, 48, "#B8A4FF", "#262042", _frame % 8 < 4 ? "#4A3D80" : "#3C3266")
    };

    static IRenderable Render()
    {
        var width = Math.Max(12, Console.WindowWidth);
        var height = Math.Max(8, Console.WindowHeight);
        var compact = width < 78 || height < 32;
        var veryCompact = width < 44 || height < 16;
        var theme = CurrentTheme();
        var choices = CurrentChoices();
        var content = _page switch
        {
            DashboardPage.Home => HomeContent(choices, theme, compact, veryCompact),
            DashboardPage.Models => ModelsContent(choices, theme, compact, veryCompact),
            _ => HelpContent(theme, compact, veryCompact)
        };

        var installedCount = Models.All.Count(m => m.Installed);
        var header = new Rows(
            Align.Center(new Markup($"[bold {theme.Accent}]◈  WHISPER  ◈[/]")),
            Align.Center(new Markup($"[grey]LOCAL SPEECH STUDIO  ·  {(installedCount > 0 ? $"[green]{installedCount} models ready[/]" : "[yellow]no models installed[/]")}[/]")));

        var footer = new Markup(veryCompact
            ? "[grey]↑↓ move  ·  Enter select  ·  Tab pages  ·  Q exit[/]"
            : $"[grey]{Spinner[_frame % Spinner.Length]}  ↑ ↓ navigate  ·  Enter select  ·  Tab pages  ·  1/2/3 switch  ·  Q exit[/]");
        var body = new Rows(header, new Text(""), content, new Text(""), Align.Center(footer));
        var panelWidth = Math.Clamp(width - 8, 10, 104);
        var panel = new Panel(body)
        {
            Width = panelWidth,
            Expand = false,
            Padding = new Padding(veryCompact ? 0 : compact ? 1 : 2, compact ? 0 : 1),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.FromHex(theme.Accent), Color.FromHex(theme.Background))
        };
        panel.Header = new PanelHeader($"[bold {theme.Accent}] {PageTitle()} [/]", Justify.Center);
        var centered = Align.Center(panel, VerticalAlignment.Middle);
        centered.Width = width;
        centered.Height = height;
        return centered;
    }

    static IRenderable HomeContent(Choice[] choices, PageTheme theme, bool compact, bool veryCompact)
    {
        if (veryCompact)
        {
            var menu = new Table().NoBorder().HideHeaders().Expand().AddColumn(new TableColumn(""));
            for (var i = 0; i < choices.Length; i++)
                menu.AddRow(ChoiceLabel(choices[i], i == _selection, theme, false));
            return new Rows(
                Align.Center(new Markup($"[bold {theme.Accent}]Speech, transcribed locally.[/]")),
                Align.Center(menu));
        }

        var heading = Align.Center(new Markup(
            $"[bold {theme.Accent}]Turn speech into text.[/]\n" +
            "[grey]Private, offline transcription — right from your terminal.[/]"));

        var grid = new Grid().Expand().AddColumn().AddColumn();
        for (var i = 0; i < choices.Length; i += 2)
        {
            var first = ChoiceCard(choices[i], i == _selection, theme, compact);
            var second = i + 1 < choices.Length
                ? ChoiceCard(choices[i + 1], i + 1 == _selection, theme, compact)
                : new Text("");
            grid.AddRow(first, second);
        }

        if (compact)
        {
            var menu = new Table().NoBorder().HideHeaders().Expand().AddColumn(new TableColumn(""));
            for (var i = 0; i < choices.Length; i++)
                menu.AddRow(ChoiceLabel(choices[i], i == _selection, theme));
            return new Rows(heading, new Text(""), Align.Center(menu));
        }

        var stats = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey).Expand()
            .AddColumn("[grey]AVAILABLE MODELS[/]")
            .AddColumn("[grey]INSTALLED[/]")
            .AddColumn("[grey]STORAGE LOCATION[/]");
        var installed = Models.All.Count(m => m.Installed);
        stats.AddRow(
            $"[bold]{Models.All.Count}[/] models",
            installed > 0 ? $"[green]{installed} ready[/]" : "[yellow]Not set up yet[/]",
            "[grey]Private · on-device[/]");

        return new Rows(heading, new Text(""), Align.Center(grid), new Text(""), Align.Center(stats));
    }

    static IRenderable ModelsContent(Choice[] choices, PageTheme theme, bool compact, bool veryCompact)
    {
        var installedCount = Models.All.Count(m => m.Installed);
        var intro = Align.Center(new Markup(
            $"[bold {theme.Accent}]Choose the right model for your workflow.[/]\n" +
            "[grey]All models run locally. Download once, use anytime.[/]"));
        if (veryCompact)
        {
            var compactMenu = new Table().NoBorder().HideHeaders().Expand().AddColumn(new TableColumn(""));
            for (var i = 0; i < choices.Length; i++)
                compactMenu.AddRow(ChoiceLabel(choices[i], i == _selection, theme, false));
            return new Rows(
                Align.Center(new Markup($"[bold {theme.Accent}]Model library[/]  [grey]{Models.All.Count} available · {installedCount} installed[/]")),
                Align.Center(compactMenu));
        }

        IRenderable modelList;
        if (compact)
        {
            modelList = new Rows(Models.All.Select(model =>
            {
                var status = model.Installed ? "[green]Installed[/]" :
                    model.HasPartial ? "[yellow]Resume available[/]" : "[grey]Not downloaded[/]";
                var size = model.Installed || model.HasPartial ? $" · {Fmt.Bytes(model.DiskBytes)}" : "";
                return (IRenderable)new Markup(
                    $"[bold {theme.Accent}]{model.Level}[/]  [grey]{Ui.Escape(model.Note)}[/]  {status}{size}");
            }));
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey).Expand()
                .AddColumn(new TableColumn("[bold]MODEL[/]").NoWrap())
                .AddColumn("[bold]PROFILE[/]")
                .AddColumn(new TableColumn("[bold]SIZE[/]").RightAligned())
                .AddColumn("[bold]STATUS[/]");

            foreach (var model in Models.All)
            {
                var size = model.Installed || model.HasPartial ? Fmt.Bytes(model.DiskBytes) : "—";
                var status = model.Installed ? "[green]Installed[/]" :
                    model.HasPartial ? "[yellow]Resume available[/]" : "[grey]Not downloaded[/]";
                table.AddRow($"[bold]{model.Level}[/]", Ui.Escape(model.Note), size, status);
            }
            modelList = table;
        }

        var menu = new Table().NoBorder().HideHeaders().Expand().AddColumn(new TableColumn(""));
        for (var i = 0; i < choices.Length; i++)
            menu.AddRow(ChoiceLabel(choices[i], i == _selection, theme));

        return compact
            ? new Rows(intro, Align.Center(modelList), Align.Center(menu))
            : new Rows(intro, new Text(""), Align.Center(modelList), new Text(""), Align.Center(menu));
    }

    static IRenderable HelpContent(PageTheme theme, bool compact, bool veryCompact)
    {
        if (veryCompact)
            return new Markup($"[bold {theme.Accent}]Whisper — offline speech-to-text[/]\n[grey]Run Whisper --help for CLI commands.[/]");

        IRenderable commands;
        if (compact)
        {
            commands = new Markup(
                "[cyan]Whisper recording.mp3[/]  [grey]Transcribe audio[/]\n" +
                "[cyan]Whisper recording.mp3 notes.srt[/]  [grey]Save subtitles[/]\n" +
                "[cyan]Whisper --mic notes.txt[/]  [grey]Live microphone[/]\n" +
                "[cyan]Whisper --download turbo[/]  [grey]Get a model[/]\n" +
                "[cyan]Whisper --uninstall[/]  [grey]Remove a model[/]");
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold]COMMAND[/]").NoWrap())
                .AddColumn("[bold]PURPOSE[/]");
            table.AddRow("[cyan]Whisper recording.mp3[/]", "Transcribe audio to the terminal.");
            table.AddRow("[cyan]Whisper recording.mp3 notes.srt[/]", "Save subtitles.");
            table.AddRow("[cyan]Whisper --mic notes.txt[/]", "Transcribe live from the microphone.");
            table.AddRow("[cyan]Whisper --download turbo[/]", "Download a model.");
            table.AddRow("[cyan]Whisper --uninstall[/]", "Open the model removal picker.");
            commands = table;
        }

        var shortcuts = new Markup(
            $"[bold {theme.Accent}]Navigation[/]\n" +
            "[grey]↑ / ↓[/]  Move between actions\n" +
            "[grey]Enter[/]  Open the selected action\n" +
            "[grey]Tab[/]  Cycle through pages\n" +
            "[grey]1 / 2 / 3[/]  Home / Models / Guide\n" +
            "[grey]Q / Esc[/]  Exit the dashboard");

        return compact
            ? new Rows(Align.Center(shortcuts), new Text(""), Align.Center(commands))
            : new Grid().Expand().AddColumn().AddColumn().AddRow(
                new Panel(commands).Header("[bold]CLI commands[/]").Border(BoxBorder.Rounded).Expand(),
                new Panel(shortcuts).Header("[bold]Keyboard shortcuts[/]").Border(BoxBorder.Rounded).Expand());
    }

    static IRenderable ChoiceCard(Choice choice, bool selected, PageTheme theme, bool compact)
    {
        var background = selected ? theme.Selected : theme.Card;
        var title = selected
            ? $"[bold white on {background}]  {Ui.Escape(choice.Title)}  [/]"
            : $"[bold {theme.Accent}]{Ui.Escape(choice.Title)}[/]";
        var detail = compact ? "" : $"[grey]{Ui.Escape(choice.Detail)}[/]";
        return new Panel(Align.Center(new Rows(new Markup(title), new Markup(detail))))
        {
            Expand = true,
            Height = compact ? 3 : 4,
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(
                Color.FromHex(selected ? theme.Accent : theme.Card),
                Color.FromHex(selected ? theme.Selected : theme.Card)),
            Padding = new Padding(1, 0)
        };
    }

    static string ChoiceLabel(Choice choice, bool selected, PageTheme theme, bool includeDetail = true) =>
        selected
            ? $"[bold white on {theme.Selected}]  ›  {Ui.Escape(choice.Title)}  [/]"
            : $"[bold {theme.Accent}]    {Ui.Escape(choice.Title)}[/]" +
              (includeDetail ? $" [grey]— {Ui.Escape(choice.Detail)}[/]" : "");

    static string PageTitle() => _page switch
    {
        DashboardPage.Models => "MODEL LIBRARY",
        DashboardPage.Help => "QUICK GUIDE",
        _ => "HOME"
    };
}
