using AsmoV2;

namespace StarChart
{
    internal class Program
    {
        static void Main(string[] args)
        {
            
            using var game = new AsmoV2.AsmoGameEngine("StarChart", 1920, 1080);
            
            // Configure window policy so the engine will scale the canvas based on window size
            game.Policy = new AsmoV2.AsmoGameEngine.WindowPolicy
            {
                AllowUserResizing = true,
                LockSize = false,
                StartFullscreen = false,
                SizeToDisplay = false,
                AltEnterToggle = true
            };
            game.HostNativeGame(new Runtime());
            game.Run();
        }
    }
}
