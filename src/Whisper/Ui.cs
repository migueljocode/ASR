static class Ui
{
    public const string Indent = "  ";
    public static readonly bool Enabled = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    static string Paint(string code, string text) => Enabled ? $"\e[{code}m{text}\e[0m" : text;

    public static string Bold(string text) => Paint("1", text);
    public static string Dim(string text) => Paint("2", text);
    public static string Red(string text) => Paint("31", text);
    public static string Green(string text) => Paint("32", text);
    public static string Yellow(string text) => Paint("33", text);
    public static string Magenta(string text) => Paint("35", text);
    public static string Cyan(string text) => Paint("36", text);

    public static void Banner() =>
        Console.WriteLine($"\n{Indent}{Magenta(Bold("● Whisper"))} {Dim("offline speech to text")}\n");

    public static void Info(string text) => Status.Print(() => Console.WriteLine($"{Indent}{Cyan("›")} {text}"));
    public static void Warn(string text) => Status.Print(() => Console.Error.WriteLine($"{Indent}{Yellow("!")} {text}"));
    public static void Error(string text) => Status.Print(() => Console.Error.WriteLine($"{Indent}{Red("✗")} {text}"));

    public static void Segment(TimeSpan? at, string text)
    {
        var stamp = at is { } t ? Dim($"[{t:hh\\:mm\\:ss}]") + " " : string.Empty;
        Status.Print(() => Console.WriteLine($"{Indent}{stamp}{Green("▌")} {text}"));
    }
}
