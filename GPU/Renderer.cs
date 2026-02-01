using System;

namespace VBlank.GPU
{
    public class Renderer
    {
        public Surface Target { get; }
        public BitmapFont Font { get; }
        public CommandBuffer CommandBuffer { get; }
        private readonly SimpleGPURunner _runner;

        public Renderer(Surface target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Font = new BitmapFont();
            CommandBuffer = new CommandBuffer();
            _runner = new SimpleGPURunner(Target);
        }

        public void Clear(uint color) => Target.Clear(color);

        public void FillRect(int x, int y, int w, int h, uint color) => Target.FillRect(x, y, w, h, color);

        public void DrawSurface(Surface src, int x, int y) => Target.Blit(src, x, y);

        // Enqueue a draw quad call (texture can be null to draw nothing)
        public void DrawQuad(Texture texture, int x, int y, int w, int h, uint tint)
        {
            CommandBuffer.Add(new DrawQuadCall(texture, x, y, w, h, tint));
        }

        // Execute all queued commands immediately
        public void Submit()
        {
            _runner.Execute(CommandBuffer);
            CommandBuffer.Clear();
        }

        public void DrawText(string text, int x, int y, uint color)
        {
            if (string.IsNullOrEmpty(text)) return;
            int cx = x;
            foreach (char c in text)
            {
                DrawChar(c, cx, y, color);
                cx += Font.CharWidth;
            }
        }

        public void DrawChar(char c, int x, int y, uint color)
        {
            var glyph = Font.GetGlyph(c);
            for (int row = 0; row < Font.CharHeight; row++)
            {
                byte bits = glyph[row];
                for (int col = 0; col < Font.CharWidth; col++)
                {
                    if ((bits & (1 << (7 - col))) != 0)
                    {
                        Target.SetPixel(x + col, y + row, color);
                    }
                }
            }
        }
    }
}
