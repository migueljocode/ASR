using System.Text.RegularExpressions;
using Spectre.Console;

static class MicPicker
{
    sealed record Mic(string Id, string Name);
    sealed record Choice(Mic? Mic);

    static readonly Regex CardLine = new(@"^card \d+: .*?\[(.+?)\], device \d+: .*?\[(.+?)\]", RegexOptions.Multiline);

    // Returns the chosen device; a requested number is 1-based, as listed.
    public static ((string Id, string Name)? Mic, bool Cancelled) Pick(int? requested)
    {
        var mics = List();
        if (requested is > 0 and var number)
        {
            if (number <= mics.Count) return ((mics[number - 1].Id, mics[number - 1].Name), false);
            Ui.Error($"Microphone {number} does not exist. Choose 1-{mics.Count}.");
            return (null, false);
        }

        if (mics.Count == 1 || !Ui.Interactive) return ((mics[0].Id, mics[0].Name), false);

        var picked = AnsiConsole.Prompt(new SelectionPrompt<Choice>()
            .Title("[bold]Choose a microphone[/]")
            .PageSize(Math.Min(mics.Count + 1, 8))
            .HighlightStyle(new Style(Color.Blue))
            .MoreChoicesText("[grey](↑/↓ to browse, Enter to select)[/]")
            .UseConverter(c => c.Mic is { } m ? $"[bold]{Ui.Escape(m.Name)}[/]" : "[grey]Cancel[/]")
            .AddChoices(mics.Select(m => new Choice(m)).Append(new Choice(null)))).Mic;

        if (picked is null)
        {
            Ui.Info("Microphone selection cancelled.");
            return (null, true);
        }

        return ((picked.Id, picked.Name), false);
    }

    // Lists the numbered microphones for --mic-device.
    public static void Show()
    {
        Ui.Card("MICROPHONES", List().Select((m, i) => ((i + 1).ToString(), $"{Ui.Escape(m.Name)}  {Ui.Dim(m.Id)}")));
        Ui.Info($"Choose one with {Ui.Bold("--mic --mic-device <number>")}, for example {Ui.Bold("Whisper --mic --mic-device 2")}");
    }

    static List<Mic> List()
    {
        List<Mic> mics = [new("default", "Default microphone")];
        try
        {
            var text = Encoding.UTF8.GetString(Proc.Output("arecord", "-l"));
            var cards = Regex.Matches(text, @"^card (\d+): .*?device (\d+):", RegexOptions.Multiline);
            var names = CardLine.Matches(text);
            for (var i = 0; i < cards.Count; i++)
                mics.Add(new($"plughw:{cards[i].Groups[1].Value},{cards[i].Groups[2].Value}", $"{names[i].Groups[1].Value} ({names[i].Groups[2].Value})"));
        }
        catch (InvalidOperationException)
        {
            // arecord missing: only the default device is offered.
        }

        return mics;
    }
}
