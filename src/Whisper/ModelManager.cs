using System.Net;
using System.Net.Http.Headers;
using Spectre.Console;

static class ModelManager
{
    static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task DownloadAsync(ModelInfo model)
    {
        if (model.Installed)
        {
            Ui.Info($"Model {Ui.Bold(model.Level.ToString())} is already installed at {Ui.Bold(model.LocalPath)}");
            return;
        }

        Directory.CreateDirectory(Models.Directory);
        var lockPath = model.LocalPath + ".lock";
        await using var modelLock = await AcquireLockAsync(lockPath);
        if (model.Installed)
        {
            Ui.Info($"Model {Ui.Bold(model.Level.ToString())} is already installed at {Ui.Bold(model.LocalPath)}");
            return;
        }

        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await FetchAsync(model);
                return;
            }
            catch (Exception e) when (attempt < maxAttempts && e is IOException or HttpRequestException)
            {
                Ui.Warn($"Download interrupted ({e.Message}). Resuming, attempt {attempt + 1}/{maxAttempts}...");
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    public static bool Uninstall(ModelInfo model)
    {
        if (!model.Installed && !model.HasPartial)
        {
            Ui.Warn($"Model '{model.Level}' is not installed.");
            return false;
        }

        File.Delete(model.LocalPath);
        File.Delete(model.PartialPath);
        Ui.Info($"Removed {Ui.Bold(model.Level.ToString())}");
        return true;
    }

    // Downloads into a stable .part file and continues from its length when restarted.
    static async Task FetchAsync(ModelInfo model)
    {
        var part = model.PartialPath;
        var offset = File.Exists(part) ? new FileInfo(part).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, model.Url);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Cancellation.Token);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            if (response.Content.Headers.ContentRange?.Length == offset)
            {
                File.Move(part, model.LocalPath, true);
                return;
            }

            File.Delete(part);
            throw new IOException("Partial file was invalid; restarting.");
        }

        response.EnsureSuccessStatusCode();

        var resumed = response.StatusCode == HttpStatusCode.PartialContent;
        if (!resumed) offset = 0;
        var total = offset + (response.Content.Headers.ContentLength ?? 0);
        long done = offset;

        await using (var source = await response.Content.ReadAsStreamAsync())
        await using (var target = new FileStream(part, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write))
        {
            if (Ui.Enabled)
            {
                await AnsiConsole.Progress()
                    .AutoClear(false)
                    .HideCompleted(false)
                    .Columns(
                        new SpinnerColumn(),
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new DownloadedColumn(),
                        new TransferSpeedColumn())
                    .StartAsync(async context =>
                    {
                        var task = context.AddTask(
                            $"[cyan]Downloading {model.Level}[/]",
                            maxValue: total > 0 ? total : double.MaxValue);
                        task.IsIndeterminate = total <= 0;
                        task.Value = offset;
                        done = offset + await CopyAsync(source, target, task);
                    });
            }
            else
            {
                Console.WriteLine($"Downloading {model.Level}...");
                done = offset + await CopyAsync(source, target, null);
            }
        }

        if (total > 0 && done != total) throw new IOException("Download ended early.");
        File.Move(part, model.LocalPath, true);
        Ui.Info($"Downloaded {Ui.Bold(model.Level.ToString())} to {Ui.Bold(model.LocalPath)}");
    }

    public static async Task<long?> SizeAsync(ModelInfo model)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Head, model.Url);
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }

    static async Task<FileStream> AcquireLockAsync(string path)
    {
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(100);
            }
        }
    }

    static async Task<long> CopyAsync(Stream source, Stream target, ProgressTask? task)
    {
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, Cancellation.Token)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), Cancellation.Token);
            done += read;
            task?.Increment(read);
        }
        return done;
    }
}
