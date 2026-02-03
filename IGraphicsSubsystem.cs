using Adamantite.GFX;
using Microsoft.Xna.Framework.Input;

namespace StarChart
{
    /// <summary>
    /// Interface for a subsystem that takes over the display and input handling.
    /// Implementation Examples: W11 Desktop, Wayland Compositor, Framebuffer Console.
    /// </summary>
    public interface IGraphicsSubsystem
    {
        /// <summary>
        /// Called during initialization to allow the subsystem to bind to the runtime.
        /// </summary>
        void Initialize(Runtime runtime);

        /// <summary>
        /// Update logic for the windowing system / compositor.
        /// </summary>
        void Update(double deltaTime);

        /// <summary>
        /// Render the composited desktop or UI into the target surface.
        /// </summary>
        void Render(Canvas target);

        /// <summary>
        /// Handle raw input from the engine.
        /// </summary>
        void ProcessInput(MouseState mouse, KeyboardState keyboard);
    }
}
