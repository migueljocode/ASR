using System.Text.RegularExpressions;
using Spectre.Console;

static class MicPicker
{
    sealed record Mic(string Id, string Name);

    static readonly Regex CardLine = new(@"^card \d+: .*?\[(.+?)\], device \d+: .*?\[(.+?)\]", RegexOptions.Multiline);

    // Returns the chosen device id and name; the number is 1-based, as listed.
    public static (string Id, string Name)? Pick(int? requested)
    {
        var mics = List();
        var number = requested ?? Ask(mics);
        if (number < 1 || number > mics.Count)
        {
            Ui.Error($"Microphone {number} does not exist. Choose 1-{mics.Count}.");
            return null;
        }

        var mic = mics[number - 1];
        return (mic.Id, mic.Name);
    }

    static int Ask(List<Mic> mics)
    {
        if (mics.Count == 1 || !Ui.Interactive) return 1;

        Ui.Info("Available microphones:");
        for (var i = 0; i < mics.Count; i++) Ui.Info($"{Ui.Bold((i + 1).ToString())}  {Ui.Escape(mics[i].Name)}");
        return AnsiConsole.Prompt(new TextPrompt<int>("[cyan]Microphone number[/]")
            .DefaultValue(1)
            .Validate(n => n >= 1 && n <= mics.Count ? ValidationResult.Success() : ValidationResult.Error($"[red]Choose 1-{mics.Count}.[/]")));
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
