using System;

namespace StarChart.stdlib.IO
{
    public static class Path
    {
        public static string Combine(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1)) return path2;
            if (string.IsNullOrEmpty(path2)) return path1;
            
            var p1 = path1.TrimEnd('/');
            var p2 = path2.TrimStart('/');
            return $"{p1}/{p2}";
        }

        public static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var idx = path.LastIndexOf('/');
            if (idx >= 0 && idx < path.Length - 1)
            {
                return path.Substring(idx + 1);
            }
            return path;
        }
        
        public static string GetExtension(string path)
        {
             if (string.IsNullOrEmpty(path)) return string.Empty;
             var idx = path.LastIndexOf('.');
             if (idx >= 0 && idx < path.Length - 1)
             {
                 return path.Substring(idx);
             }
             return string.Empty;
        }
        
        public static string GetDirectoryName(string path)
        {
             if (string.IsNullOrEmpty(path)) return string.Empty;
             var idx = path.LastIndexOf('/');
             if (idx > 0)
             {
                 return path.Substring(0, idx);
             }
             if (idx == 0) return "/";
             return string.Empty;
        }
    }
}
