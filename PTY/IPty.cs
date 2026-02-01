using System;
using Microsoft.Xna.Framework.Input;

namespace StarChart.PTY
{
    // Minimal PTY interface for sessions
    public interface IPty
    {
        // Program -> PTY (output that should be displayed on the terminal)
        void WriteToPty(char c);

        // PTY -> Program (user input). Raised when the user submits input (e.g. presses Enter).
        event EventHandler<char> OnInput;

        // Resize the pseudo-terminal (columns, rows)
        void Resize(int cols, int rows);

        void HandleKey(Keys key, bool shift);
    }
}
