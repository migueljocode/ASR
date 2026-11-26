using System.Net;
using System.Net.Http.Headers;

static class ModelManager
{
    static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task DownloadAsync(ModelInfo model)
    {
        if (model.Installed)
        {
            Console.WriteLine($"Model '{model.Level}' is already installed at {model.LocalPath}");
            return;
        }

        Directory.CreateDirectory(Models.Directory);
        var lockPath = model.LocalPath + ".lock";
        await using var modelLock = await AcquireLockAsync(lockPath);
        if (model.Installed)
        {
            Console.WriteLine($"Model '{model.Level}' is already installed at {model.LocalPath}");
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
                Console.WriteLine();
                Ui.Warn($"Download interrupted ({e.Message}). Resuming, attempt {attempt + 1}/{maxAttempts}...");
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    public static void Uninstall(ModelInfo model)
    {
        if (!model.Installed && !model.HasPartial)
        {
            Ui.Warn($"Model '{model.Level}' is not installed.");
            return;
        }

        File.Delete(model.LocalPath);
        File.Delete(model.PartialPath);
        Ui.Info($"Removed {Ui.Bold(model.Level.ToString())}");
    }

    // Downloads into a stable .part file and continues from its length when restarted.
    static async Task FetchAsync(ModelInfo model)
    {
        var part = model.PartialPath;
        var offset = File.Exists(part) ? new FileInfo(part).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, model.Url);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

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
        var (buffer, done, clock, lastDraw) = (new byte[81920], offset, Stopwatch.StartNew(), TimeSpan.Zero);

        await using (var source = await response.Content.ReadAsStreamAsync())
        await using (var target = new FileStream(part, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write))
        {
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                if (clock.Elapsed - lastDraw < TimeSpan.FromMilliseconds(100)) continue;
                lastDraw = clock.Elapsed;
                DrawBar(done, total, clock.Elapsed, offset);
            }
        }

        DrawBar(done, total, clock.Elapsed, offset);
        Console.WriteLine();
        if (total > 0 && done != total) throw new IOException("Download ended early.");
        File.Move(part, model.LocalPath, true);
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

    static void DrawBar(long done, long total, TimeSpan elapsed, long offset)
    {
        const int width = 30;
        var fraction = total > 0 ? (double)done / total : 0;
        var filled = (int)(fraction * width);
        var speed = (long)((done - offset) / Math.Max(elapsed.TotalSeconds, 0.001));
        Console.Write($"\r[{new string('#', filled)}{new string('-', width - filled)}] {fraction,4:P0}  {Fmt.Bytes(done)} / {Fmt.Bytes(total)}  {Fmt.Bytes(speed)}/s   ");
    }
}
