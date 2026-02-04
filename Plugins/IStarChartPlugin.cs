using System;

namespace StarChart.Plugins
{
    /// <summary>
    /// Context provided to plugins when they are initialized.
    /// </summary>
    public class PluginContext
    {
        // Windowing system provided by the host (implementing IStarChartWindowSystem)
        public IStarChartWindowSystem? WindowingContext { get; set; }
        // Host services provided by the runtime for plugins to request host-level actions
        // (e.g. request the graphical UI, replace the window system).
        public IHostServices? HostServices { get; set; }
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
    /// Services exposed by the host/runtime to plugins.
    /// </summary>
    public interface IHostServices
    {
        /// <summary>
        /// Request that the runtime start or switch to the graphical UI.
        /// </summary>
        void RequestGraphicalStart();

        /// <summary>
        /// Replace the current window system with the provided implementation.
        /// </summary>
        void SetWindowSystem(IStarChartWindowSystem? system, object? rootHandle = null);

        /// <summary>
        /// Get the current window system in use (may be null).
        /// </summary>
        IStarChartWindowSystem? CurrentWindowSystem { get; }

        /// <summary>
        /// Schedule an action to run on the host/main thread (render/update loop).
        /// Useful for background apps that need to perform UI or windowing operations.
        /// </summary>
        void InvokeOnMainThread(Action action);
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

    /// <summary>
    /// Interface for programs that want to control individual windows provided by the host.
    /// Plugins can cast `PluginContext.WindowingContext` to this type to open/close windows
    /// and attach content or receive basic events.
    /// </summary>
    public interface IStarChartWindowSystem
    {
        /// <summary>
        /// Open a new window with the specified title. Returns an opaque window handle.
        /// </summary>
        object OpenWindow(string title, int width, int height);

        /// <summary>
        /// Close the given window handle.
        /// </summary>
        void CloseWindow(object windowHandle);

        /// <summary>
        /// Set the window's title.
        /// </summary>
        void SetTitle(object windowHandle, string title);

        /// <summary>
        /// Attach an opaque content object to the window (host-defined semantics).
        /// </summary>
        void AttachContent(object windowHandle, object? content);

        /// <summary>
        /// Event raised when a window is closed by the host or user.
        /// </summary>
        event Action<object>? WindowClosed;
    }
}
