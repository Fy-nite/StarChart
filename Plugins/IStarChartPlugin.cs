using System;
using StarChart.stdlib.W11;

namespace StarChart.Plugins
{
    /// <summary>
    /// Context provided to plugins when they are initialized.
    /// </summary>
    public class PluginContext
    {
        public DisplayServer? DisplayServer { get; set; }
        public Adamantite.VFS.VfsManager? VFS { get; set; }
        public string[] Arguments { get; set; } = Array.Empty<string>();
        public string WorkingDirectory { get; set; } = "/";
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
    /// Interface for StarChart applications.
    /// </summary>
    public interface IStarChartApp : IStarChartPlugin
    {
        /// <summary>
        /// Main window of the application (if any).
        /// </summary>
        Window? MainWindow { get; }
    }

    /// <summary>
    /// Interface for StarChart window managers.
    /// </summary>
    public interface IStarChartWindowManager : IStarChartPlugin
    {
        /// <summary>
        /// Update the window manager (called each frame if needed).
        /// </summary>
        void Update();

        /// <summary>
        /// Handle mouse input.
        /// </summary>
        void HandleMouse(int x, int y, bool leftDown, bool leftPressed, bool leftReleased);
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
}
