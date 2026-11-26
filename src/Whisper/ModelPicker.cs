static class ModelPicker
{
    public static ModelInfo? FindInstalled(Precision? requested, bool mic)
    {
        var available = Models.All.Where(m => m.Installed).ToArray();
        var recommended = mic ? Precision.Turbo : Precision.Large;

        if (available.Length == 0)
        {
            var name = (requested ?? recommended).ToString().ToLowerInvariant();
            Ui.Error("No speech model is installed.");
            Ui.Info($"Download one with: {Ui.Bold("Whisper --download " + name)}");
            return null;
        }

        var model = requested is { } level ? available.FirstOrDefault(m => m.Level == level) : null;
        if (requested is { } missing && model is null)
            Ui.Warn($"'{missing}' is not installed. Download it with: Whisper --download {missing.ToString().ToLowerInvariant()}");

        model ??= Choose(available, recommended);
        Ui.Info($"Model: {Ui.Bold(model.Level.ToString())} {Ui.Dim(model.Note)}");
        return model;
    }

    static ModelInfo Choose(ModelInfo[] available, Precision recommended)
    {
        var fallback = available.FirstOrDefault(m => m.Level == recommended) ?? available[^1];
        if (available.Length == 1 || Console.IsInputRedirected) return fallback;

        Console.WriteLine($"{Ui.Indent}{Ui.Bold("Select a model")}");
        for (var i = 0; i < available.Length; i++)
        {
            var m = available[i];
            var mark = m == fallback ? Ui.Green(" (default)") : string.Empty;
            Console.WriteLine($"{Ui.Indent}  {Ui.Cyan($"[{i + 1}]")} {m.Level,-7} {Ui.Dim(m.Note)}{mark}");
        }

        return ReadIndex(available.Length, "default") is { } index ? available[index] : fallback;
    }

    public static async Task<ModelInfo?> ChooseForDownloadAsync()
    {
        var sizes = await Task.WhenAll(Models.All.Select(ModelManager.SizeAsync));

        Console.WriteLine($"{Ui.Indent}{Ui.Bold("Download a model")}");
        for (var i = 0; i < Models.All.Count; i++)
        {
            var m = Models.All[i];
            var size = sizes[i] is { } bytes ? Fmt.Bytes(bytes) : "? MB";
            var mark = m.Installed ? Ui.Green(" (installed)") : string.Empty;
            Console.WriteLine($"{Ui.Indent}  {Ui.Cyan($"[{i + 1}]")} {m.Level,-7} {size,9}  {Ui.Dim(m.Note)}{mark}");
        }

        if (Console.IsInputRedirected)
        {
            Ui.Info($"Pick one with: {Ui.Bold("Whisper --download <model>")}");
            return null;
        }

        return ReadIndex(Models.All.Count, "cancel") is { } index ? Models.All[index] : null;
    }

    public static ModelInfo? ChooseForUninstall()
    {
        var removable = Models.All.Where(m => m.Installed || m.HasPartial).ToArray();
        if (removable.Length == 0)
        {
            Ui.Info("No models are installed.");
            return null;
        }

        Console.WriteLine($"{Ui.Indent}{Ui.Bold("Uninstall a model")}");
        for (var i = 0; i < removable.Length; i++)
        {
            var m = removable[i];
            var mark = m.Installed ? string.Empty : Ui.Yellow(" (partial download)");
            Console.WriteLine($"{Ui.Indent}  {Ui.Cyan($"[{i + 1}]")} {m.Level,-7} {Fmt.Bytes(m.DiskBytes),9}  {Ui.Dim(m.Note)}{mark}");
        }

        if (Console.IsInputRedirected)
        {
            Ui.Info($"Pick one with: {Ui.Bold("Whisper --uninstall <model>")}");
            return null;
        }

        if (ReadIndex(removable.Length, "cancel") is not { } index) return null;
        var picked = removable[index];
        Console.Write($"{Ui.Indent}{Ui.Cyan("›")} Delete {picked.Level}? [y/N]: ");
        return Console.ReadLine()?.Trim().ToLowerInvariant() is "y" or "yes" ? picked : null;
    }

    static int? ReadIndex(int count, string emptyMeans)
    {
        while (true)
        {
            Console.Write($"{Ui.Indent}{Ui.Cyan("›")} Choice [1-{count}], Enter = {emptyMeans}: ");
            var text = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            if (int.TryParse(text, out var n) && n >= 1 && n <= count) return n - 1;
            Ui.Warn("Invalid choice.");
        }
    }
}
