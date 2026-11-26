using System.ComponentModel;

static class Proc
{
    public static Process Start(string file, params string[] args)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);

        try
        {
            return Process.Start(info)!;
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException($"'{file}' was not found. Install it first (see README).");
        }
    }

    public static byte[] Output(string file, params string[] args)
    {
        using var process = Start(file, args);
        using var stdout = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(stdout);
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"'{file}' exited with code {process.ExitCode}.");
        return stdout.ToArray();
    }
}
