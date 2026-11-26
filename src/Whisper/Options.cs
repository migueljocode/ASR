enum Format { Txt, Srt }
enum Precision { Tiny, Base, Small, Medium, Turbo, Large }

record Options(string? Input, string? Output, bool Mic, bool Help, Precision? Level, string Language, bool Download, bool Uninstall, string? Error)
{
    public Format Format => ".srt".Equals(Path.GetExtension(Output), StringComparison.OrdinalIgnoreCase) ? Format.Srt : Format.Txt;

    public static Options Parse(string[] args)
    {
        List<string> files = [];
        string? input = null;
        Precision? level = null;
        var (mic, help, language, download, uninstall) = (false, false, "auto", false, false);

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help": help = true; break;
                case "--mic": mic = true; break;
                case "-i" or "--input":
                    if (!TryGetValue(args, ref i, out input!))
                        return Invalid("Missing file after -i.");
                    break;
                case "--download":
                    download = true;
                    if (i + 1 >= args.Length || args[i + 1].StartsWith('-')) break;
                    var downloadName = args[++i];
                    if (!Enum.TryParse(downloadName, true, out Precision downloadLevel))
                        return Invalid($"Unknown model '{downloadName}'.");
                    level = downloadLevel;
                    break;
                case "--uninstall":
                    uninstall = true;
                    if (i + 1 >= args.Length || args[i + 1].StartsWith('-')) break;
                    var uninstallName = args[++i];
                    if (!Enum.TryParse(uninstallName, true, out Precision uninstallLevel))
                        return Invalid($"Unknown model '{uninstallName}'.");
                    level = uninstallLevel;
                    break;
                case "-p" or "--model":
                    if (!TryGetValue(args, ref i, out var modelName))
                        return Invalid("Missing model name after -p.");
                    if (!Enum.TryParse(modelName, true, out Precision parsedLevel))
                        return Invalid($"Unknown model '{modelName}'.");
                    level = parsedLevel;
                    break;
                case "-l" or "--language":
                    if (!TryGetValue(args, ref i, out language))
                        return Invalid("Missing language after -l.");
                    break;
                default: files.Add(args[i]); break;
            }
        }

        if (download && (mic || files.Count > 0)) return Invalid("--download cannot be combined with transcription input.");
        if (download && args.Any(a => a is "-l" or "--language")) return Invalid("--download cannot be combined with a language option.");
        if (uninstall && (download || mic || input is not null || files.Count > 0)) return Invalid("--uninstall cannot be combined with other options.");

        var positional = input is null ? files : files.Prepend(input).ToList();
        var (first, second) = (positional.ElementAtOrDefault(0), positional.ElementAtOrDefault(1));
        return new(mic ? null : first, mic ? first : second, mic, help, level, language, download, uninstall, null);
    }

    static bool TryGetValue(string[] args, ref int index, out string value)
    {
        if (index + 1 < args.Length)
        {
            value = args[++index];
            return true;
        }

        value = string.Empty;
        return false;
    }

    static Options Invalid(string error) => new(null, null, false, false, null, "auto", false, false, error);
}
