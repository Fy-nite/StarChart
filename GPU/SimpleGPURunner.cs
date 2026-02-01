using System;

namespace VBlank.GPU
{
    // Executes commands from a CommandBuffer onto a target Surface
    public class SimpleGPURunner
    {
        private readonly Surface _target;

        public SimpleGPURunner(Surface target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void Execute(CommandBuffer cb)
        {
            if (cb == null) return;

            foreach (var cmd in cb.Commands)
            {
                switch (cmd)
                {
                    case DrawQuadCall dq:
                        if (dq.Texture?.Surface != null)
                        {
                            _target.DrawTexturedQuad(dq.Texture.Surface, dq.X, dq.Y, dq.Width, dq.Height, dq.Tint);
                        }
                        break;
                }
            }
        }
    }
}
