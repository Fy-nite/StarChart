using System;
using System.IO;
using System.Text;
using Adamantite.VFS;

namespace StarChart.stdlib.IO
{
    public static class File
    {
        public static bool Exists(string path)
        {
            return VFSGlobal.Manager?.Exists(path) ?? false;
        }

        public static void Delete(string path)
        {
            VFSGlobal.Manager?.Delete(path);
        }

        public static byte[] ReadAllBytes(string path)
        {
            if (VFSGlobal.Manager == null) throw new IOException("VFS not initialized");
            return VFSGlobal.Manager.ReadAllBytes(path);
        }

        public static string ReadAllText(string path)
        {
            var bytes = ReadAllBytes(path);
            return Encoding.UTF8.GetString(bytes);
        }

        public static void WriteAllBytes(string path, byte[] bytes)
        {
            if (VFSGlobal.Manager == null) throw new IOException("VFS not initialized");
            VFSGlobal.Manager.WriteAllBytes(path, bytes);
        }

        public static void WriteAllText(string path, string contents)
        {
            var bytes = Encoding.UTF8.GetBytes(contents);
            WriteAllBytes(path, bytes);
        }

        public static Stream OpenRead(string path)
        {
            if (VFSGlobal.Manager == null) throw new IOException("VFS not initialized");
            return VFSGlobal.Manager.OpenRead(path);
        }
        
        public static Stream OpenWrite(string path)
        {
            if (VFSGlobal.Manager == null) throw new IOException("VFS not initialized");
            return VFSGlobal.Manager.OpenWrite(path);
        }

        public static void Create(string path)
        {
             if (VFSGlobal.Manager == null) throw new IOException("VFS not initialized");
             VFSGlobal.Manager.CreateFile(path);
        }

        public static void Copy(string sourceFileName, string destFileName)
        {
            var data = ReadAllBytes(sourceFileName);
            WriteAllBytes(destFileName, data);
        }

        public static void Move(string sourceFileName, string destFileName)
        {
            Copy(sourceFileName, destFileName);
            Delete(sourceFileName);
        }

        public static void AppendAllText(string path, string contents)
        {
            string original = "";
            if (Exists(path))
            {
                original = ReadAllText(path);
            }
            WriteAllText(path, original + contents);
        }
    }
}
