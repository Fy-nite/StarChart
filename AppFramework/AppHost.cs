using System;
using StarChart.Plugins;

namespace StarChart.AppFramework
{
    // Minimal host helpers for running StarChart applications.
    public static class AppHost
    {
        // Initialize and start an app using the provided context.
        public static void Run(IStarChartApp app, PluginContext ctx, StarChart.Scheduler? scheduler = null)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            // If a primary PTY is provided in the context, create a TerminalHost wrapper
            if (ctx.PrimaryPty != null && ctx.TerminalHost == null)
            {
                ctx.TerminalHost = new TerminalHost(ctx.PrimaryPty);
            }
            if (scheduler != null)
                ctx.Scheduler = scheduler;
            app.Initialize(ctx);
            app.Start();

            // Run the scheduler update loop if provided
            if (scheduler != null)
            {
                // Example: simple update loop (replace with host/game engine loop in production)
                double lastTime = Environment.TickCount;
                while (true)
                {
                    double now = Environment.TickCount;
                    double delta = (now - lastTime) / 1000.0;
                    lastTime = now;
                    scheduler.Update(delta);
                    System.Threading.Thread.Sleep(16); // ~60Hz
                    // Optionally break loop on app exit/stop
                    // if (app.IsStopped) break;
                }
            }
        }

        // Stop an app.
        public static void Stop(IStarChartApp app)
        {
            if (app == null) return;
            try { app.Stop(); } catch { }
        }
    }
}
