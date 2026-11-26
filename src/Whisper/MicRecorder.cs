static class MicRecorder
{
    const int BytesPerSecond = Wav.Rate * 2;
    const int BlockBytes = BytesPerSecond / 50;               // 20 ms capture blocks
    const int MinChunk = BytesPerSecond * 6 / 10, MaxChunk = 8 * BytesPerSecond;
    const int PauseBytes = BytesPerSecond * 3 / 10, PreRollBytes = BytesPerSecond * 3 / 10;

    // Low latency: 0.3 s pause ends a phrase, 0.3 s pre-roll keeps first words.
    // Capture never blocks on inference: chunks queue up and are transcribed one by one.
    public static async IAsyncEnumerable<Stream> ChunksAsync(string device, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<byte[]>();
        using var arecord = Proc.Start("arecord", "-D", device, "-q", "-t", "raw", "-f", "S16_LE", "-r", Wav.Rate.ToString(), "-c", "1");
        var capture = CaptureAsync(arecord.StandardOutput.BaseStream, channel.Writer);

        Ui.Info("Listening... press " + Ui.Bold("Enter") + " to stop.");
        Status.Mode(Capture.Listening);
        _ = Task.Run(() =>
        {
            Console.ReadLine();
            if (!arecord.HasExited) arecord.Kill();
        }, ct);

        await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
        {
            if (channel.Reader.Count > 1) Ui.Warn("falling behind - choose a smaller model");
            yield return Wav.FromPcm(chunk);
        }

        await capture;
    }

    static async Task CaptureAsync(Stream pcm, ChannelWriter<byte[]> writer)
    {
        var block = new byte[BlockBytes];
        List<byte> buffer = [], preRoll = [];
        var (silent, voiced) = (0, false);

        void Flush()
        {
            if (voiced) writer.TryWrite(buffer.ToArray());
            buffer.Clear();
            (silent, voiced) = (0, false);
            Status.Mode(Capture.Listening);
        }

        try
        {
            int read;
            while ((read = await pcm.ReadAsync(block)) > 0)
            {
                byte[] data = block[..read];
                var loud = Chunker.IsLoud(data);
                silent = loud ? 0 : silent + read;

                if (!voiced && !loud)
                {
                    preRoll.AddRange(data);
                    if (preRoll.Count > PreRollBytes) preRoll.RemoveRange(0, preRoll.Count - PreRollBytes);
                    continue;
                }

                if (!voiced)
                {
                    buffer.AddRange(preRoll);
                    preRoll.Clear();
                    voiced = true;
                    Status.Mode(Capture.Recording);
                }

                buffer.AddRange(data);
                if (buffer.Count >= MaxChunk || (buffer.Count >= MinChunk && silent >= PauseBytes)) Flush();
            }

            Flush();
        }
        finally
        {
            writer.Complete();
        }
    }
}
