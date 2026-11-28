using System.Text.RegularExpressions;

sealed record ComputeInfo(string Cpu, int Threads, string Library, string? Gpu, string? Backend, string[] Log)
{
    public bool UsesGpu => Gpu is not null;
    public string Label => UsesGpu ? $"GPU ({Backend})" : "CPU";
    public string Device => Gpu ?? Cpu;
}

static class Hardware
{
    // Reads the backend and device that whisper.cpp reports while loading the model.
    public static ComputeInfo Describe(RuntimeLibrary? library, IEnumerable<string> log, int threads, bool useGpu)
    {
        var lines = log.ToArray();
        var name = useGpu ? library?.ToString() ?? "unknown" : nameof(RuntimeLibrary.Cpu);
        var usesGpu = library is not null && !name.StartsWith("Cpu", StringComparison.OrdinalIgnoreCase);
        var device = Find(lines, @"ggml_vulkan: 0 = ([^|]+)") ?? (usesGpu ? VulkanDevice() : null);
        return new(CpuName(), threads, name, usesGpu ? device ?? $"{name} device" : null, name, lines);
    }

    // The loader reports the backend but not the device, so ask Vulkan directly (skipping software renderers).
    static string? VulkanDevice()
    {
        try
        {
            var info = new ProcessStartInfo("vulkaninfo", "--summary")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return Find(output.Split('\n'), @"deviceName\s*=\s*(?!llvmpipe)(.+)");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    static string? Find(IEnumerable<string> lines, string pattern) =>
        lines.Select(line => Regex.Match(line, pattern))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value.Trim())
            .FirstOrDefault();

    static string CpuName()
    {
        const string key = "model name";
        if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
                if (line.StartsWith(key, StringComparison.Ordinal))
                    return line[(line.IndexOf(':') + 1)..].Trim();

        return RuntimeInformation.ProcessArchitecture.ToString();
    }
}
