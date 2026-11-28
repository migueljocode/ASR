enum Capture { Idle, Listening, Recording }

// Status bar pinned to the last terminal row; output scrolls in the region above it.
static class Status
{
    static readonly string[] Spinner = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    const string Bg = "\e[48;2;12;60;90m", Reset = "\e[0m";
    static readonly object Gate = new();
    static Thread? _thread;
    static volatile bool _running;
    static Capture _capture;
    static string? _work;
    static int _frame, _height;

    // Own thread, so the animation keeps running while the thread pool is busy with inference.
    public static void Start()
    {
        if (!Ui.Enabled || _thread is not null) return;
        lock (Gate)
        {
            Console.Write("\e[?25l");
            _thread = new Thread(Animate) { IsBackground = true, Name = "status" };
            Reserve();
        }

        _running = true;
        _thread.Start();
    }

    public static void Stop()
    {
        if (_thread is not { } thread) return;
        _running = false;
        thread.Join();
        lock (Gate)
        {
            Console.Write($"\e7\e[{_height};1H\e[2K\e[r\e8\e[?25h");
            _thread = null;
        }
    }

    static void Animate()
    {
        while (_running)
        {
            Thread.Sleep(70);
            lock (Gate)
            {
                if (!_running) break;
                _frame++;
                Draw();
            }
        }
    }

    public static void Mode(Capture capture) { lock (Gate) { _capture = capture; Draw(); } }
    public static void Work(string? work) { lock (Gate) { _work = work; Draw(); } }

    public static void Print(Action write)
    {
        lock (Gate) write();
    }

    // Limits scrolling to every row but the last, keeping the cursor where it was.
    static void Reserve()
    {
        _height = Console.WindowHeight;
        Console.Write($"\e7\e[1;{_height - 1}r\e8");
        if (Console.CursorTop >= _height - 1) Console.SetCursorPosition(0, _height - 2);
    }

    static void Draw()
    {
        if (_thread is null) return;
        if (_height != Console.WindowHeight) Reserve();

        List<(string Text, string Color)> parts = [(" ◆ Whisper", "\e[96m")];
        if (_capture == Capture.Recording) parts.Add(("  ● Recording", "\e[91m"));
        else if (_capture == Capture.Listening) parts.Add(($"  {(_frame / 7 % 2 == 0 ? "●" : "○")} Listening", "\e[96m"));
        if (_work is not null) parts.Add(($"  {Spinner[_frame % Spinner.Length]} {_work}", "\e[93m"));
        parts.Add(("  ·  Esc stop", "\e[37m"));

        var width = Console.WindowWidth;
        var text = new StringBuilder(Bg);
        var used = 0;
        foreach (var (part, color) in parts)
        {
            var piece = part[..Math.Min(part.Length, Math.Max(0, width - used))];
            text.Append(color).Append(piece);
            used += piece.Length;
        }

        text.Append(' ', Math.Max(0, width - used)).Append(Reset);
        Console.Write($"\e7\e[{_height};1H{text}\e8");
    }
}
