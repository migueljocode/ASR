static class Fmt
{
    public static string Bytes(long n) => n switch
    {
        >= 1L << 30 => $"{n / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{n / (double)(1L << 20):F0} MB",
        _ => $"{n / 1024.0:F0} KB"
    };
}
