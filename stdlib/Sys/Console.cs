using System;

namespace StarChart.stdlib.Sys
{
    public interface IConsoleBackend
    {
        void Write(string value);
        void WriteLine(string value);
        void Clear();
        string ReadLine();
    }

    public class SystemConsoleBackend : IConsoleBackend
    {
        public void Write(string value) => System.Console.Write(value);
        public void WriteLine(string value) => System.Console.WriteLine(value);
        public void Clear() => System.Console.Clear();
        public string ReadLine() => System.Console.ReadLine();
    }

    public static class Console
    {
        private static IConsoleBackend _backend;

        public static IConsoleBackend Backend
        {
            get => _backend ??= new SystemConsoleBackend();
            set => _backend = value;
        }

        public static void Write(string value) => Backend.Write(value);
        public static void Write(object value) => Backend.Write(value?.ToString());
        public static void WriteLine(string value) => Backend.WriteLine(value);
        public static void WriteLine(object value) => Backend.WriteLine(value?.ToString());
        public static void WriteLine() => Backend.WriteLine("");
        public static void Clear() => Backend.Clear();
        
        public static string ReadLine() => Backend.ReadLine();
    }
}
