using System;
using System.Collections.Generic;
using System.Linq;
using StarChart.PTY;

namespace StarChart.AppFramework
{
    // TerminalHost multiplexes a physical PTY and lets plugins subscribe to input.
    // The most recently added subscriber is treated as the foreground and receives input.
    public class TerminalHost : IPty
    {
        readonly IPty _physical;
        readonly List<EventHandler<char>> _handlers = new List<EventHandler<char>>();

        public event EventHandler<char> OnInput
        {
            add
            {
                lock (_handlers) { _handlers.Add(value); }
            }
            remove
            {
                lock (_handlers) { _handlers.Remove(value); }
            }
        }

        public TerminalHost(IPty physical)
        {
            _physical = physical ?? throw new ArgumentNullException(nameof(physical));
            // forward physical input to foreground handler
            _physical.OnInput += (s, c) =>
            {
                EventHandler<char>? h = null;
                lock (_handlers)
                {
                    if (_handlers.Count > 0) h = _handlers.Last();
                }
                h?.Invoke(this, c);
            };
        }

        // Acquire exclusive terminal mode. Returns a token which, while not disposed,
        // reserves the terminal for guest apps. Guest apps should still subscribe to
        // `TerminalHost.OnInput` to receive input; acquiring exclusive mode prevents
        // races with the shell but does not by itself deliver input to the guest.
        public IDisposable AcquireExclusive()
        {
            EventHandler<char> marker = (_, __) => { };
            lock (_handlers)
            {
                _handlers.Add(marker);
            }
            return new Releaser(this, marker);
        }

        class Releaser : IDisposable
        {
            readonly TerminalHost _t;
            readonly EventHandler<char> _m;
            public Releaser(TerminalHost t, EventHandler<char> m) { _t = t; _m = m; }
            public void Dispose()
            {
                lock (_t._handlers) { _t._handlers.Remove(_m); }
            }
        }

        public void WriteToPty(char c)
        {
            _physical.WriteToPty(c);
        }

        public void Resize(int cols, int rows)
        {
            _physical.Resize(cols, rows);
        }

        public void HandleKey(Microsoft.Xna.Framework.Input.Keys key, bool shift)
        {
            _physical.HandleKey(key, shift);
        }
    }
}
