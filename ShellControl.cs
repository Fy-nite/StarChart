namespace StarChart
{
    // Simple global control flags set by shells to influence runtime startup behavior.
    public static class ShellControl
    {
        // When true the Program should start the Runtime in graphical mode
        // (i.e. prefer the W11 windowing system instead of attaching a fullscreen VT).
        public static bool StartGraphicalRequested = false;
    }
}
