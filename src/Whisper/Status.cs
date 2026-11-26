enum Capture { Idle, Listening, Recording }

// Single animated status line pinned below the output: capture state | background work.
static class Status
{
    static readonly string[] Spinner = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    static readonly object Gate = new();
    static CancellationTokenSource? _cts;
    static Task _loop = Task.CompletedTask;
    static Capture _capture;
    static string? _work;
    static int _frame;

    public static void Start()
    {
        if (!Ui.Enabled || _cts is not null) return;
        Console.Write("\e[?25l");
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(70));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                    lock (Gate) { _frame++; Draw(); }
            }
            catch (OperationCanceledException) { }
        });
    }

    public static void Stop()
    {
        if (_cts is null) return;
        _cts.Cancel();
        _loop.Wait();
        lock (Gate)
        {
            Clear();
            _cts = null;
            Console.Write("\e[?25h");
        }
    }

    public static void Mode(Capture capture) { lock (Gate) { _capture = capture; Draw(); } }
    public static void Work(string? work) { lock (Gate) { _work = work; Draw(); } }

    public static void Print(Action write)
    {
        lock (Gate)
        {
            Clear();
            write();
            Draw();
        }
    }

    static void Clear()
    {
        if (_cts is not null) Console.Write("\r\e[2K");
    }

    static void Draw()
    {
        if (_cts is null) return;
        List<string> parts = [];
        if (_capture == Capture.Recording) parts.Add($"{Ui.Red("●")} {Ui.Bold(Ui.Red("Recording"))}");
        else if (_capture == Capture.Listening) parts.Add($"{Ui.Cyan(_frame / 7 % 2 == 0 ? "●" : "○")} {Ui.Cyan("Listening")}");
        if (_work is not null) parts.Add($"{Ui.Yellow(Spinner[_frame % Spinner.Length])} {Ui.Yellow(_work)}");
        Console.Write($"\r\e[2K{Ui.Indent}{string.Join(Ui.Dim("  │  "), parts)}");
    }
}
