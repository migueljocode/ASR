record ModelInfo(Precision Level, string FileName, string Note)
{
    public string Url => $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{FileName}";
    public string LocalPath => Path.Combine(Models.Directory, FileName);
    public string PartialPath => LocalPath + ".part";
    public bool Installed => File.Exists(LocalPath);
    public bool HasPartial => File.Exists(PartialPath);
    public long DiskBytes => new FileInfo(Installed ? LocalPath : PartialPath).Length;
}

static class Models
{
    public static readonly string Directory = GetDirectory();

    public static readonly IReadOnlyList<ModelInfo> All =
    [
        new(Precision.Tiny, "ggml-tiny.bin", "fastest, poor accuracy"),
        new(Precision.Base, "ggml-base.bin", "fast, weak for fa/tr"),
        new(Precision.Small, "ggml-small.bin", "balanced"),
        new(Precision.Medium, "ggml-medium.bin", "good accuracy"),
        new(Precision.Turbo, "ggml-large-v3-turbo.bin", "near-large accuracy, much faster"),
        new(Precision.Large, "ggml-large-v3.bin", "best accuracy, slowest")
    ];

    public static ModelInfo Get(Precision level) => All.First(m => m.Level == level);

    static string GetDirectory()
    {
        var root = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(root, "whisper", "models");
    }
}
