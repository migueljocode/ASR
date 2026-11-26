static class MicPicker
{
    public static string Pick()
    {
        Ui.Info("Using the default microphone (ALSA/PipeWire).");
        return "default";
    }
}
