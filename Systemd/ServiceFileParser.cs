using System;
using System.Collections.Generic;
using System.IO;

namespace StarChart.Systemd
{
    /// <summary>
    /// Tiny parser for simple unit files in key=value form. Not meant to be full systemd compatibility.
    /// Example:
    /// Name=foo
    /// Description=Example
    /// ExecStart=asm:/bin/hello.asm
    /// After=network.service,foo.service
    /// WantedBy=multi-user.target
    /// </summary>
    public static class ServiceFileParser
    {
        public static ServiceUnit Parse(string text)
        {
            var unit = new ServiceUnit();
            using var sr = new StringReader(text);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("#")) continue;
                var idx = line.IndexOf('=');
                if (idx < 0) continue;
                var key = line.Substring(0, idx).Trim();
                var val = line.Substring(idx + 1).Trim();
                switch (key)
                {
                    case "Name": unit.Name = val; break;
                    case "Description": unit.Description = val; break;
                    case "ExecStart": unit.ExecStart = val; break;
                    case "After": unit.After.AddRange(ParseList(val)); break;
                    case "WantedBy": unit.WantedBy.AddRange(ParseList(val)); break;
                }
            }
            return unit;
        }

        static IEnumerable<string> ParseList(string v)
        {
            foreach (var p in v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                yield return p.Trim();
        }
    }
}
