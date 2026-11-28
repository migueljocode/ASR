enum Format { Txt, Srt }
enum Precision { Tiny, Base, Small, Medium, Turbo, Large }
enum ComputeDevice { Auto, Cpu, Gpu }

record Options(string? Input, string? Output, bool Mic, bool Help, Precision? Level, string Language, bool Download, bool Uninstall, ComputeDevice Device, bool Benchmark, int? MicDevice, string? Error)
{
    public Format Format => ".srt".Equals(Path.GetExtension(Output), StringComparison.OrdinalIgnoreCase) ? Format.Srt : Format.Txt;

    public const string Languages = "en, fa, tr, ru, ar, de, fr, es, zh, ja";

    public static string[] SplitLanguages(string language) =>
        language.Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToLowerInvariant())
            .Distinct()
            .ToArray();

    public static Options Parse(string[] args)
    {
        List<string> files = [];
        string? input = null;
        Precision? level = null;
        var (mic, help, language, download, uninstall) = (false, false, "auto", false, false);
        var (device, benchmark) = (ComputeDevice.Auto, false);
        int? micDevice = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help": help = true; break;
                case "--mic":
                    mic = true;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var number) && number > 0)
                    {
                        micDevice = number;
                        i++;
                    }
                    break;
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
                case "-m" or "-p" or "--model":
                    if (!TryGetValue(args, ref i, out var modelName))
                        return Invalid("Missing model name after -m/-p.");
                    if (!Enum.TryParse(modelName, true, out Precision parsedLevel))
                        return Invalid($"Unknown model '{modelName}'.");
                    level = parsedLevel;
                    break;
                case "--benchmark": benchmark = true; break;
                case "--mic-device":
                    micDevice = 0;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var micNumber) && micNumber > 0)
                    {
                        micDevice = micNumber;
                        i++;
                    }
                    break;
                case "-d" or "--device":
                    if (!TryGetValue(args, ref i, out var deviceName))
                        return Invalid("Missing device after -d.");
                    if (!Enum.TryParse(deviceName, true, out device))
                        return Invalid($"Unknown device '{deviceName}'. Use auto, cpu, or gpu.");
                    break;
                case "-l" or "--language":
                    if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
                        return Invalid($"Missing language after -l. Common codes: {Languages}.");
                    language = args[++i];
                    break;
                case var option when option.StartsWith('-') && option.Length > 1:
                    return Invalid($"Unknown option '{option}'.");
                default: files.Add(args[i]); break;
            }
        }

        if (download && (mic || files.Count > 0)) return Invalid("--download cannot be combined with transcription input.");
        if (download && args.Any(a => a is "-l" or "--language")) return Invalid("--download cannot be combined with a language option.");
        if (benchmark && (mic || download || uninstall)) return Invalid("--benchmark cannot be combined with --mic, --download or --uninstall.");
        if (uninstall && (download || mic || input is not null || files.Count > 0)) return Invalid("--uninstall cannot be combined with other options.");

        if (micDevice is > 0 && !mic) return Invalid("--mic-device needs --mic.");

        var languages = SplitLanguages(language);
        if (languages.Length == 0 || languages.Length > 1 && languages.Contains("auto"))
            return Invalid("Use a language code such as en, or a combination such as fa+en.");

        var positional = input is null ? files : files.Prepend(input).ToList();
        var (first, second) = (positional.ElementAtOrDefault(0), positional.ElementAtOrDefault(1));
        if (benchmark && first is null) return Invalid("--benchmark needs an audio file.");
        return new(mic ? null : first, mic ? first : second, mic, help, level, language, download, uninstall, device, benchmark, micDevice, null);
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

    static Options Invalid(string error) => new(null, null, false, false, null, "auto", false, false, ComputeDevice.Auto, false, null, error);
}
