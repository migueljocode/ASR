static class AudioConverter
{
    // ffmpeg decodes any format (mp3, m4a, ogg, mp4, ...) to raw 16 kHz mono PCM.
    public static byte[] ToPcm(string input) =>
        Proc.Output("ffmpeg", "-v", "error", "-i", input, "-vn",
            "-f", "s16le", "-ac", "1", "-ar", Wav.Rate.ToString(), "-");
}
