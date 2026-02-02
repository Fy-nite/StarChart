using System;
using System.Collections.Generic;

namespace StarChart.stdlib.Sys
{
    public static class Env
    {
        private static Dictionary<string, string> _vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static Env()
        {
            _vars["OS"] = "StarChart";
            _vars["VERSION"] = "0.1.0";
            _vars["USER"] = "user";
            _vars["HOME"] = "/home/user";
            _vars["PATH"] = "/bin;/usr/bin";
        }

        public static void Set(string variable, string value)
        {
            _vars[variable] = value;
        }

        public static string Get(string variable)
        {
            if (_vars.TryGetValue(variable, out var val))
            {
                return val;
            }
            return string.Empty;
        }

        public static Dictionary<string, string>.KeyCollection GetKeys()
        {
            return _vars.Keys;
        }
    }
}
