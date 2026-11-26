static class Usage
{
    public const string Text = """
        Whisper - offline speech to text

        Usage:
          Whisper <input> [output]   Transcribe a file. Output .txt or .srt (by extension).
                                     No output = print to console only.
          Whisper --mic [output]     Live microphone (system default ALSA/PipeWire device).
                                     Text prints as you speak; Enter stops.
                                     With output = also saved to that file (.txt only).
          Whisper -h | --help        Show this help.

        Options:
          -p <precision>   tiny | base | small | medium | turbo | large
                           Uses that model when installed; otherwise falls back to one that is.
          -l <language>    auto | en | fa | tr | ...              (default: auto)
          --download [model]
                           Download a model once for this user; shared by all app installs.
                           Without a model: show download sizes and pick one interactively.
          --uninstall [model]
                           Remove a downloaded model. Without a model: pick one interactively.

        Examples:
          Whisper talk.mp3 talk.txt
          Whisper talk.mp3 talk.srt -p medium -l fa
          Whisper --mic notes.txt -l en
          Whisper --download turbo
        """;
}
