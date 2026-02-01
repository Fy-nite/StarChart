using System;
using StarChart.Plugins;

namespace StarChart.AppFramework
{
    // Minimal host helpers for running StarChart applications.
    public static class AppHost
    {
        // Initialize and start an app using the provided context.
        public static void Run(IStarChartApp app, PluginContext ctx)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            // If a primary PTY is provided in the context, create a TerminalHost wrapper
            if (ctx.PrimaryPty != null && ctx.TerminalHost == null)
            {
                ctx.TerminalHost = new TerminalHost(ctx.PrimaryPty);
            }
            app.Initialize(ctx);
            app.Start();
        }

        // Stop an app.
        public static void Stop(IStarChartApp app)
        {
            if (app == null) return;
            try { app.Stop(); } catch { }
        }
    }
}
