using System.Text.RegularExpressions;
using Spectre.Console;

sealed record MicDevice(string Id, string Name);

static class MicPicker
{
    static readonly MicDevice Default = new("default", "System default (ALSA/PipeWire)");
    static readonly Regex CardLine = new(@"^card (\d+): .*?\[(.+?)\], device (\d+): .*?\[(.+?)\]", RegexOptions.Multiline);

    // Uses the requested device (id or part of its name), or asks when several exist.
    public static MicDevice? Pick(string? requested)
    {
        var devices = List();
        if (requested is not null) return Find(devices, requested);
        if (devices.Count == 1 || !Ui.Interactive) return Default;

        return AnsiConsole.Prompt(new SelectionPrompt<MicDevice>()
            .Title("[bold]Choose a microphone[/]")
            .PageSize(Math.Min(devices.Count, 10))
            .HighlightStyle(new Style(Color.Blue))
            .MoreChoicesText("[grey](↑/↓ to browse, Enter to select)[/]")
            .UseConverter(d => $"[bold]{Ui.Escape(d.Name)}[/]  [grey]{Ui.Escape(d.Id)}[/]")
            .AddChoices(devices));
    }

    static MicDevice? Find(List<MicDevice> devices, string requested)
    {
        var match = devices.FirstOrDefault(d => d.Id.Equals(requested, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault(d => d.Name.Contains(requested, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        Ui.Error($"Microphone '{requested}' was not found. Available:");
        foreach (var d in devices) Ui.Info($"{Ui.Bold(d.Id)} {Ui.Dim(d.Name)}");
        return null;
    }

    static List<MicDevice> List()
    {
        List<MicDevice> devices = [Default];
        string text;
        try
        {
            text = Encoding.UTF8.GetString(Proc.Output("arecord", "-l"));
        }
        catch (InvalidOperationException)
        {
            return devices;
        }

        foreach (Match m in CardLine.Matches(text))
            devices.Add(new($"plughw:{m.Groups[1].Value},{m.Groups[3].Value}", $"{m.Groups[2].Value} - {m.Groups[4].Value}"));
        return devices;
    }
}
