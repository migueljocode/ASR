using Spectre.Console;

static class ModelPicker
{
    sealed record PickerChoice(ModelInfo? Model);

    public static (ModelInfo? Model, bool Cancelled) FindInstalled(Precision? requested, bool mic)
    {
        var available = Models.All.Where(m => m.Installed).ToArray();
        var recommended = mic ? Precision.Turbo : Precision.Large;

        if (available.Length == 0)
        {
            var name = (requested ?? recommended).ToString().ToLowerInvariant();
            Ui.Error("No speech model is installed.");
            Ui.Info($"Download one with: {Ui.Bold("Whisper --download " + name)}");
            return (null, false);
        }

        var model = requested is { } level ? available.FirstOrDefault(m => m.Level == level) : null;
        if (requested is { } missing && model is null)
            Ui.Warn($"'{missing}' is not installed. Download it with: Whisper --download {missing.ToString().ToLowerInvariant()}");

        model ??= Choose(available, recommended);
        if (model is null)
        {
            Ui.Info("Model selection cancelled.");
            return (null, true);
        }

        return (model, false);
    }

    static ModelInfo? Choose(ModelInfo[] available, Precision recommended)
    {
        var fallback = available.FirstOrDefault(m => m.Level == recommended) ?? available[^1];
        if (available.Length == 1 || !Ui.Interactive) return fallback;

        Ui.NewPage();
        var table = CreateModelTable();
        foreach (var model in available)
        {
            var mark = model == fallback ? "[green]Recommended[/]" : "";
            table.AddRow(
                $"[bold]{model.Level}[/]",
                Ui.Escape(model.Note),
                model == fallback ? mark : "");
        }
        AnsiConsole.Write(Align.Center(table));

        var choices = available.OrderByDescending(m => m == fallback).Select(m => new PickerChoice(m)).Append(new PickerChoice(null));
        return AnsiConsole.Prompt(new SelectionPrompt<PickerChoice>()
            .Title("[bold]Choose a model[/]")
            .PageSize(Math.Min(available.Length + 1, 8))
            .HighlightStyle(new Style(Color.Blue))
            .MoreChoicesText("[grey](↑/↓ to browse, Enter to select)[/]")
            .UseConverter(choice => choice.Model is not { } m
                ? "[grey]Cancel[/]"
                : $"{(m == fallback ? "[green]★ Recommended[/] " : "")}[bold]{m.Level}[/]  [grey]{Ui.Escape(m.Note)}[/]")
            .AddChoices(choices)).Model;
    }

    public static async Task<ModelInfo?> ChooseForDownloadAsync()
    {
        var sizes = await Task.WhenAll(Models.All.Select(ModelManager.SizeAsync));
        AnsiConsole.Write(new Rule("[bold]Download a model[/]").RuleStyle("grey"));

        var table = CreateModelTable();
        table.AddColumn(new TableColumn("[bold]Download[/]").RightAligned());
        foreach (var (model, size) in Models.All.Zip(sizes))
        {
            var status = model.Installed
                ? "[green]Installed[/]"
                : model.HasPartial ? "[yellow]Partial download[/]" : "[grey]Available[/]";
            var downloadSize = size is { } bytes ? Fmt.Bytes(bytes) : "Size unavailable";
            table.AddRow(
                $"[bold]{model.Level}[/]",
                Ui.Escape(model.Note),
                status,
                Ui.Escape(downloadSize));
        }
        AnsiConsole.Write(Align.Center(table));

        if (!Ui.Interactive)
        {
            Ui.Info($"Pick one with: {Ui.Bold("Whisper --download <model>")}");
            return null;
        }

        var choices = Models.All.Select(m => new PickerChoice(m)).Append(new PickerChoice(null));
        return AnsiConsole.Prompt(new SelectionPrompt<PickerChoice>()
            .Title("[bold]Select a model to download[/]")
            .PageSize(Math.Min(Models.All.Count + 1, 8))
            .HighlightStyle(new Style(Color.Blue))
            .MoreChoicesText("[grey](↑/↓ to browse, Enter to select)[/]")
            .UseConverter(choice => choice.Model is not { } m
                ? "[grey]Cancel[/]"
                : $"[bold]{m.Level}[/]  [grey]{Ui.Escape(m.Note)}[/]")
            .AddChoices(choices)).Model;
    }

    public static ModelInfo? ChooseForUninstall()
    {
        var removable = Models.All.Where(m => m.Installed || m.HasPartial).ToArray();
        if (removable.Length == 0)
        {
            Ui.Info("No models are installed.");
            return null;
        }

        AnsiConsole.Write(new Rule("[bold]Uninstall a model[/]").RuleStyle("grey"));
        var table = CreateModelTable();
        table.AddColumn(new TableColumn("[bold]On disk[/]").RightAligned());
        foreach (var model in removable)
        {
            var status = model.Installed ? "[green]Installed[/]" : "[yellow]Partial download[/]";
            table.AddRow(
                $"[bold]{model.Level}[/]",
                Ui.Escape(model.Note),
                status,
                Ui.Escape(Fmt.Bytes(model.DiskBytes)));
        }
        AnsiConsole.Write(Align.Center(table));

        if (!Ui.Interactive)
        {
            Ui.Info($"Pick one with: {Ui.Bold("Whisper --uninstall <model>")}");
            return null;
        }

        var choices = removable.Select(m => new PickerChoice(m)).Append(new PickerChoice(null));
        var picked = AnsiConsole.Prompt(new SelectionPrompt<PickerChoice>()
            .Title("[bold]Select a model to remove[/]")
            .PageSize(Math.Min(removable.Length + 1, 8))
            .HighlightStyle(new Style(Color.Blue))
            .MoreChoicesText("[grey](↑/↓ to browse, Enter to select)[/]")
            .UseConverter(choice => choice.Model is not { } m
                ? "[grey]Cancel[/]"
                : $"[bold]{m.Level}[/]  [grey]{Ui.Escape(m.Note)}[/]")
            .AddChoices(choices)).Model;

        return picked is not null && AnsiConsole.Confirm($"[yellow]Remove {picked.Level} model and its files?[/]", false)
            ? picked
            : null;
    }

    static Table CreateModelTable() =>
        new Table()
            .Width(Ui.ContentWidth)
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Model[/]").NoWrap())
            .AddColumn("[bold]Profile[/]")
            .AddColumn("[bold]Status[/]");
}
