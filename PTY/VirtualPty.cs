using System;
using System.Text;
using Adamantite.GPU;
using Microsoft.Xna.Framework.Input;

namespace StarChart.PTY
{
    // Simple PTY implementation that writes program output into a VirtualTerminal
    // and converts terminal key events into input bytes for listeners.
    public class VirtualPty : IPty
    {
        readonly VirtualTerminal _vt;

        public event EventHandler<char> OnInput;

        public VirtualPty(VirtualTerminal vt)
        {
            _vt = vt ?? throw new ArgumentNullException(nameof(vt));
        }

        public void WriteToPty(char c)
        {
            _vt.Write(c.ToString());
        }

        public void Resize(int cols, int rows)
        {
            _vt.Resize(cols, rows);
        }

        // Expose a helper to accept key events from the runtime and translate them
        // into chars raised via OnInput. This maps simple printable keys.
        public void HandleKey(Keys key, bool shift)
        {
            if (key == Keys.Enter)
            {
                OnInput?.Invoke(this, '\n');
                _vt.Write('\n'.ToString());
                return;
            }

            if (key == Keys.Back)
            {
                OnInput?.Invoke(this, '\b');
                _vt.HandleKey(key, shift);
                return;
            }

            if (key == Keys.Tab)
            {
                OnInput?.Invoke(this, '\t');
                _vt.Write('\t'.ToString());
                return;
            }

            if (TryMapKey(key, shift, out var ch))
            {
                OnInput?.Invoke(this, ch);
                _vt.Write(ch.ToString());
            }
        }

        static bool TryMapKey(Keys key, bool shift, out char ch)
        {
            ch = '\0';

            if (key >= Keys.A && key <= Keys.Z)
            {
                ch = (char)(key - Keys.A + (shift ? 'A' : 'a'));
                return true;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                if (shift)
                {
                    char[] shifted = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                    ch = shifted[key - Keys.D0];
                }
                else
                {
                    ch = (char)(key - Keys.D0 + '0');
                }
                return true;
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                ch = (char)(key - Keys.NumPad0 + '0');
                return true;
            }

            switch (key)
            {
                case Keys.Space: ch = ' '; break;
                case Keys.OemMinus: ch = shift ? '_' : '-'; break;
                case Keys.OemPlus: ch = shift ? '+' : '='; break;
                case Keys.OemOpenBrackets: ch = shift ? '{' : '['; break;
                case Keys.OemCloseBrackets: ch = shift ? '}' : ']'; break;
                case Keys.OemPipe: ch = shift ? '|' : '\\'; break;
                case Keys.OemSemicolon: ch = shift ? ':' : ';'; break;
                case Keys.OemQuotes: ch = shift ? '"' : '\''; break;
                case Keys.OemComma: ch = shift ? '<' : ','; break;
                case Keys.OemPeriod: ch = shift ? '>' : '.'; break;
                case Keys.OemQuestion: ch = shift ? '?' : '/'; break;
                case Keys.OemTilde: ch = shift ? '~' : '`'; break;
                default: return false;
            }

            return true;
        }
    }
}
