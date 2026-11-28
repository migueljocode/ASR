// Full-screen mode like `less`: runs on the alternate buffer and restores the terminal on exit.
static class Screen
{
    static bool _active;

    public static bool Active => _active;

    public static void Enter()
    {
        if (!Ui.Interactive || _active) return;
        _active = true;
        Console.Write("\e[?1049h\e[H");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Leave();
    }

    public static void Leave()
    {
        if (!_active) return;
        _active = false;
        Console.Write("\e[?1049l");
        Console.Out.Flush();
    }
}
