using System;

namespace StarChart.Plugins
{
    /// <summary>
    /// Context provided to plugins when they are initialized.
    /// </summary>
    public class PluginContext
    {
        // Opaque context for the windowing system (e.g. W11 DisplayServer)
        public object? WindowingContext { get; set; }
        
        // The active graphics subsystem
        public IGraphicsSubsystem? Graphics { get; set; }

        public Adamantite.VFS.VfsManager? VFS { get; set; }

        public string[] Arguments { get; set; } = Array.Empty<string>();
        public string WorkingDirectory { get; set; } = "/";
        // PATH entries made available to plugins (VFS paths)
        public string[] Path { get; set; } = Array.Empty<string>();
        // Primary PTY provided by the host (physical terminal)
        public StarChart.PTY.IPty? PrimaryPty { get; set; }
        // TerminalHost provided by the host to allow plugins to take control of IO
        public StarChart.AppFramework.TerminalHost? TerminalHost { get; set; }
        // Scheduler provided by the host for apps/plugins to register tasks
        public StarChart.Scheduler? Scheduler { get; set; }
    }

    /// <summary>
    /// Base interface for all StarChart plugins.
    /// </summary>
    public interface IStarChartPlugin
    {
        /// <summary>
        /// Initialize the plugin with the given context.
        /// </summary>
        void Initialize(PluginContext context);

        /// <summary>
        /// Start the plugin (called after Initialize).
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the plugin and clean up resources.
        /// </summary>
        void Stop();
    }

    /// <summary>
    /// Interface for StarChart applications (Generic).
    /// </summary>
    public interface IStarChartApp : IStarChartPlugin
    {
        /// <summary>
        /// Main window of the application (if any).
        /// Returns opaque object (typically StarChart.stdlib.W11.Windowing.Window).
        /// </summary>
        object? MainWindow { get; }
    }





    /// <summary>
    /// Interface for StarChart desktop environments.
    /// </summary>
    public interface IStarChartDesktopEnvironment : IStarChartPlugin
    {
        /// <summary>
        /// The window manager component (if any).
        /// </summary>
        IStarChartWindowManager? WindowManager { get; }

        /// <summary>
        /// Update the desktop environment.
        /// </summary>
        void Update();
    }

    /// <summary>
    /// Interface for StarChart games.
    /// </summary>
    public interface IStarChartGame : IStarChartPlugin
    {
        /// <summary>
        /// Update the game logic and rendering.
        /// </summary>
        void Update(double deltaTime);

        /// <summary>
        /// Handle input events.
        /// </summary>
        void HandleInput(int mouseX, int mouseY, bool[] mouseButtons, bool[] keys);
    }

    /// <summary>
    /// Interface for StarChart background services.
    /// </summary>
    public interface IStarChartService : IStarChartPlugin
    {
        /// <summary>
        /// Update the service (called periodically).
        /// </summary>
        void Update();
    }

    /// <summary>
    /// Interface for StarChart window managers.
    /// </summary>
    public interface IStarChartWindowManager : IStarChartPlugin
    {
        /// <summary>
        /// Called every frame to update the window manager logic.
        /// </summary>
        void Update();

        /// <summary>
        /// Handle mouse input.
        /// </summary>
        void HandleMouse(int x, int y, bool leftDown, bool leftPressed, bool leftReleased);
    }
}
