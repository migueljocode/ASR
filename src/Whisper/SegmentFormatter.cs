static class SegmentFormatter
{
    public static string Render(SegmentData s, int index, Format format, TimeSpan offset) => format switch
    {
        Format.Srt => $"{index}\n{(s.Start + offset):hh\\:mm\\:ss\\,fff} --> {(s.End + offset):hh\\:mm\\:ss\\,fff}\n{s.Text.Trim()}\n",
        _ => s.Text.Trim()
    };
}
