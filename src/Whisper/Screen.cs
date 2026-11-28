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

    // Keeps the full-screen view until Enter, Esc or Ctrl+X.
    public static async Task HoldAsync()
    {
        if (!_active) return;
        Console.WriteLine();
        Cancellation.Watch();
        Cancellation.Finish();
        Ui.Info("Press " + Ui.Bold("Enter") + ", " + Ui.Bold("Esc") + " or " + Ui.Bold("Ctrl+X") + " to exit.");
        try
        {
            await Task.Delay(Timeout.Infinite, Cancellation.Abort);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public static void Leave()
    {
        if (!_active) return;
        _active = false;
        Console.Write("\e[?1049l");
        Console.Out.Flush();
    }
}
