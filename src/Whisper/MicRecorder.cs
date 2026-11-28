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
        var channel = Channel.CreateUnbounded<(byte[] Pcm, int Generation)>();
        using var arecord = Proc.Start("arecord", "-D", device, "-q", "-t", "raw", "-f", "S16_LE", "-r", Wav.Rate.ToString(), "-c", "1");
        var capture = CaptureAsync(arecord.StandardOutput.BaseStream, channel.Writer);

        Ui.Blank();
        Ui.Info("Listening... " + Ui.Bold("Enter") + " clears the buffer, " + Ui.Bold("Esc") + " stops.");
        Ui.Blank();
        Status.Mode(Capture.Listening);
        using var stop = Cancellation.Token.Register(() =>
        {
            if (!arecord.HasExited) arecord.Kill();
        });

        await foreach (var (pcm, generation) in channel.Reader.ReadAllAsync(ct))
        {
            if (generation != Cancellation.Generation) continue;
            if (channel.Reader.Count > 1) Ui.Warn("falling behind - choose a smaller model");
            yield return Wav.FromPcm(pcm);
        }

        await capture;
    }

    static async Task CaptureAsync(Stream pcm, ChannelWriter<(byte[] Pcm, int Generation)> writer)
    {
        var block = new byte[BlockBytes];
        List<byte> buffer = [], preRoll = [];
        var (silent, voiced) = (0, false);

        void Flush()
        {
            if (voiced) writer.TryWrite((buffer.ToArray(), Cancellation.Generation));
            buffer.Clear();
            (silent, voiced) = (0, false);
            Status.Mode(Capture.Listening);
        }

        try
        {
            var seen = Cancellation.Generation;
            int read;
            while ((read = await pcm.ReadAsync(block)) > 0)
            {
                if (seen != Cancellation.Generation)
                {
                    seen = Cancellation.Generation;
                    voiced = false;
                    Flush();
                    preRoll.Clear();
                }

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
