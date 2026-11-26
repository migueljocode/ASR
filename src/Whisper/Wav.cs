static class Wav
{
    public const int Rate = 16000;
    static readonly WaveFormat Pcm16kMono = new(Rate, 16, 1);

    // Whisper expects 16 kHz, 16-bit, mono WAV.
    public static MemoryStream FromPcm(byte[] pcm)
    {
        using var ms = new MemoryStream();
        using (var writer = new WaveFileWriter(ms, Pcm16kMono))
            writer.Write(pcm, 0, pcm.Length);
        return new MemoryStream(ms.ToArray());
    }
}
