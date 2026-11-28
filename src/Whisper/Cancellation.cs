static class Cancellation
{
    static readonly CancellationTokenSource Source = new(), Hard = new();

    public static CancellationToken Token => Source.Token;

    // Cancelled by Esc / Ctrl+C only: interrupts transcription itself, not just capture.
    public static CancellationToken Abort => Hard.Token;

    static volatile bool _finished;
    static CancellationTokenSource _flush = new();
    static int _generation;

    // Bumped on every buffer clear; audio captured under an older generation is stale.
    public static int Generation => Volatile.Read(ref _generation);

    // Cancelled when Enter clears the live buffer; each press swaps in a fresh source.
    public static CancellationToken Flush => _flush.Token;

    static void ClearBuffer()
    {
        Interlocked.Increment(ref _generation);
        Interlocked.Exchange(ref _flush, new()).Cancel();
        Ui.Info(Ui.Dim("Buffer cleared. Listening..."));
    }

    // Once the work is done, Enter exits as well.
    public static void Finish() => _finished = true;

    static void CancelAll()
    {
        Source.Cancel();
        Hard.Cancel();
    }

    // Esc, Ctrl+X or Ctrl+C aborts; Enter clears the live buffer (enterClears) or exits once the work is finished.
    public static void Watch(bool enterClears = false)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = !Source.IsCancellationRequested;
            CancelAll();
        };

        if (!Ui.Interactive) return;
        _ = Task.Run(async () =>
        {
            while (!Hard.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var pressed = Console.ReadKey(true);
                    var ctrlX = pressed.Key == ConsoleKey.X && pressed.Modifiers.HasFlag(ConsoleModifiers.Control);
                    if (pressed.Key == ConsoleKey.Escape || ctrlX) CancelAll();
                    else if (pressed.Key == ConsoleKey.Enter && _finished) CancelAll();
                    else if (pressed.Key == ConsoleKey.Enter && enterClears) ClearBuffer();
                }

                await Task.Delay(50);
            }
        });
    }
}
