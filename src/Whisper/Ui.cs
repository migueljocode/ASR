using Spectre.Console;

static class Ui
{
    public static int ContentWidth => Interactive ? Math.Clamp(Console.WindowWidth - 4, 20, 84) : 84;
    public static string Indent => new(' ', Interactive ? Math.Max(2, (Console.WindowWidth - ContentWidth) / 2) : 2);
    public static readonly bool Interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
    public static readonly bool Enabled = Interactive && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    public static string Escape(string text) => Markup.Escape(text);

    public static string Bold(string text) => $"[bold]{Escape(text)}[/]";
    public static string Dim(string text) => $"[grey]{Escape(text)}[/]";
    public static string Red(string text) => $"[red]{Escape(text)}[/]";
    public static string Green(string text) => $"[green]{Escape(text)}[/]";
    public static string Yellow(string text) => $"[yellow]{Escape(text)}[/]";
    public static string Magenta(string text) => $"[magenta]{Escape(text)}[/]";
    public static string Cyan(string text) => $"[cyan]{Escape(text)}[/]";

    public static void Banner()
    {
        AnsiConsole.Write(new Rule("[bold #c4b5fd]WHISPER[/]  [grey]OFFLINE SPEECH RECOGNITION[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();
    }

    // Compact recap printed on the normal screen after full-screen mode ends.
    public static void Recap(string text) => AnsiConsole.MarkupLine($"  [cyan]›[/] {text}");

    // Clears the full-screen view and redraws the banner; does nothing outside full-screen mode.
    public static void NewPage()
    {
        if (!Screen.Active) return;
        Console.Write("\e[2J\e[H");
        Banner();
    }

    public static void Info(string text) =>
        Status.Print(() => AnsiConsole.MarkupLine($"{Indent}[cyan]›[/] {text}"));

    public static void Warn(string text) =>
        Status.Print(() => AnsiConsole.MarkupLine($"{Indent}[yellow]![/] {Escape(text)}"));

    public static void Error(string text)
    {
        Screen.Leave();
        Status.Print(() => Console.Error.WriteLine($"{Indent}✗ {text}"));
    }

    public static void Segment(TimeSpan? at, string text)
    {
        var stamp = at is { } t ? Dim($"[{t:hh\\:mm\\:ss}]") + " " : string.Empty;
        Status.Print(() => AnsiConsole.MarkupLine($"{Indent}{stamp}[green]▌[/] {Escape(text)}"));
    }
}
