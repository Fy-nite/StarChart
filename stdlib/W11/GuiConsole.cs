using System;
using StarChart.stdlib.Sys;
using StarChart.stdlib.W11;
using Adamantite.GPU;

namespace StarChart.stdlib.W11
{
    public class XTermConsoleBackend : IConsoleBackend
    {
        private readonly XTerm _term;

        public XTermConsoleBackend(XTerm term)
        {
            _term = term;
        }

        public void Write(string value) => _term.Write(value);
        public void WriteLine(string value) => _term.WriteLine(value);
        public void Clear() => _term.SetLines(null); // Or add Clear to XTerm

        public string ReadLine()
        {
            // Blocking read not supported on UI thread
            return null; 
        }
    }

    public class VirtualTerminalConsoleBackend : IConsoleBackend
    {
        private readonly VirtualTerminal _term;

        public VirtualTerminalConsoleBackend(VirtualTerminal term)
        {
            _term = term;
        }

        public void Write(string value) => _term.Write(value);
        public void WriteLine(string value) => _term.WriteLine(value);
        public void Clear() 
        {
             // VT doesn't have Clear exposed directly maybe?
             // It does have logic to clear, or we can just print many newlines?
             // Or implement Clear in VT.
             // For now do nothing or write newlines.
             _term.Write("\f"); // Form feed often clears?
        }

        public string ReadLine()
        {
            return null;
        }
    }
}
