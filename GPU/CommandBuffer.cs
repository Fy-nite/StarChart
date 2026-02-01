using System;
using System.Collections.Generic;

namespace VBlank.GPU
{
    // Simple command buffer for GPU-style drawcalls
    public class CommandBuffer
    {
        private readonly List<ICommand> _commands = new();

        public void Clear() => _commands.Clear();

        public void Add(ICommand cmd)
        {
            if (cmd == null) throw new ArgumentNullException(nameof(cmd));
            _commands.Add(cmd);
        }

        public IReadOnlyList<ICommand> Commands => _commands;
    }

    public interface ICommand { }

    public struct DrawQuadCall : ICommand
    {
        public Texture Texture;
        public int X, Y, Width, Height;
        public uint Tint; // multiply tint

        public DrawQuadCall(Texture tex, int x, int y, int w, int h, uint tint)
        {
            Texture = tex;
            X = x;
            Y = y;
            Width = w;
            Height = h;
            Tint = tint;
        }
    }
}
