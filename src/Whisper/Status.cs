using Spectre.Console;

enum Capture { Idle, Listening, Recording }

// Single animated status line pinned below the output: capture state | background work.
static class Status
{
    static readonly string[] Spinner = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    static readonly object Gate = new();
    static Thread? _thread;
    static volatile bool _running;
    static Capture _capture;
    static string? _work;
    static int _frame;
    static readonly StringWriter Buffer = new();
    static readonly IAnsiConsole Renderer = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.Yes,
        ColorSystem = ColorSystemSupport.EightBit,
        Interactive = InteractionSupport.No,
        Out = new AnsiConsoleOutput(Buffer)
    });

    // Own thread, so the animation keeps running while the thread pool is busy with inference.
    public static void Start()
    {
        if (!Ui.Enabled || _thread is not null) return;
        Console.Write("\e[?25l");
        _running = true;
        _thread = new Thread(Animate) { IsBackground = true, Name = "status" };
        _thread.Start();
    }

    public static void Stop()
    {
        if (_thread is not { } thread) return;
        _running = false;
        thread.Join();
        lock (Gate)
        {
            Clear();
            _thread = null;
            Console.Write("\e[?25h");
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
        lock (Gate)
        {
            Clear();
            write();
            Draw();
        }
    }

    static void Clear()
    {
        if (_thread is not null) Console.Write("\r\e[2K");
    }

    static void Draw()
    {
        if (_thread is null) return;
        List<string> parts = [];
        if (_capture == Capture.Recording) parts.Add("[bold red]● Recording[/]");
        else if (_capture == Capture.Listening) parts.Add($"[cyan]{(_frame / 7 % 2 == 0 ? "●" : "○")} Listening[/]");
        if (_work is not null) parts.Add($"{Ui.Yellow(Spinner[_frame % Spinner.Length])} {Ui.Yellow(_work)}");
        Buffer.GetStringBuilder().Clear();
        Renderer.Markup(string.Join("[grey]  │  [/]", parts));
        Console.Write($"\r{Ui.Indent}{Buffer}\e[K");
    }
}
