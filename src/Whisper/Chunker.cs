static class Chunker
{
    const int BytesPerSecond = Wav.Rate * 2;
    const int BlockBytes = BytesPerSecond / 20;               // 50 ms blocks
    const int MinChunk = BytesPerSecond, MaxChunk = 25 * BytesPerSecond;
    const int PauseBytes = BytesPerSecond * 2 / 5;
    const double VoiceRms = 150;

    public static bool IsLoud(ReadOnlySpan<byte> pcm) => Rms(pcm) > VoiceRms;

    // Split at pauses so Whisper detects the language of each phrase on its own.
    public static IEnumerable<(byte[] Pcm, TimeSpan Start)> Split(byte[] pcm)
    {
        var (begin, silent, voiced) = (0, 0, false);
        for (var pos = 0; pos < pcm.Length; pos += BlockBytes)
        {
            var end = Math.Min(pos + BlockBytes, pcm.Length);
            var loud = IsLoud(pcm.AsSpan(pos, end - pos));
            (silent, voiced) = (loud ? 0 : silent + end - pos, voiced || loud);

            var length = end - begin;
            if (length < MinChunk || (silent < PauseBytes && length < MaxChunk)) continue;
            if (voiced) yield return (pcm[begin..end], StartOf(begin));
            (begin, silent, voiced) = (end, 0, false);
        }

        if (voiced) yield return (pcm[begin..], StartOf(begin));
    }

    static TimeSpan StartOf(int byteOffset) => TimeSpan.FromSeconds((double)byteOffset / BytesPerSecond);

    static double Rms(ReadOnlySpan<byte> bytes)
    {
        var sum = 0.0;
        var samples = MemoryMarshal.Cast<byte, short>(bytes);
        foreach (var s in samples) sum += (double)s * s;
        return Math.Sqrt(sum / Math.Max(1, samples.Length));
    }
}
