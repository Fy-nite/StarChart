using System;
using StarChart.stdlib.W11;

namespace StarChart.stdlib.W11
{
    // Minimal login shell that requires the user to type `startw` to start the W11 environment.
    public class LoginShell
    {
        readonly XTerm _term;
        readonly Runtime _runtime;

        public LoginShell(XTerm term, Runtime runtime)
        {
            _term = term ?? throw new ArgumentNullException(nameof(term));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _term.OnEnter += OnEnter;
            _term.WriteLine("Welcome. Type 'startw' to start the W11 session.");
        }

        void OnEnter(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            var cmd = line.Trim();
            if (string.Equals(cmd, "startw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cmd, "startW", StringComparison.OrdinalIgnoreCase))
            {
                _term.WriteLine("Starting W11...");
                try
                {
                    _runtime.StartW11();
                    _term.WriteLine("W11 started.");
                }
                catch (Exception ex)
                {
                    _term.WriteLine("Failed to start W11: " + ex.Message);
                }
                return;
            }

            _term.WriteLine("Unknown command. Type 'startw' to start W11.");
        }
    }
}
