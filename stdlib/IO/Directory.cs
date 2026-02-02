using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adamantite.VFS;

namespace StarChart.stdlib.IO
{
    public static class Directory
    {
        public static void CreateDirectory(string path)
        {
            VFSGlobal.Manager?.CreateDirectory(path);
        }

        public static bool Exists(string path)
        {
            var info = VFSGlobal.Manager?.GetFileInfo(path);
            return info != null && info.IsDirectory;
        }

        public static string[] GetFiles(string path)
        {
            if (VFSGlobal.Manager == null) return Array.Empty<string>();
            return VFSGlobal.Manager.Enumerate(path)
                .Where(f => !f.IsDirectory)
                .Select(f => f.Path)
                .ToArray();
        }

        public static string[] GetDirectories(string path)
        {
             if (VFSGlobal.Manager == null) return Array.Empty<string>();
            return VFSGlobal.Manager.Enumerate(path)
                .Where(f => f.IsDirectory)
                .Select(f => f.Path)
                .ToArray();
        }

        public static void Delete(string path, bool recursive)
        {
            if (!recursive)
            {
                // Check if empty? VFS might not enforce, but stdlib should try to be safe?
                // For now, just call delete on the dir.
                VFSGlobal.Manager?.Delete(path);
                return;
            }

            foreach (var file in GetFiles(path))
            {
                var fullPath = Path.Combine(path, file); // GetFiles returns relative? No, let's check VFS.
                // VFS.Enumerate returns VfsFileInfo.Path.
                // InMemoryFileSystem.Enumerate returns "file.txt" inside "dir".
                // Wait, InMemoryFileSystem.Enumerate:
                // k.Substring(prefix.Length) -> returns RELATIVE path from the listed dir.
                // So if I enum "foo", and have "foo/bar.txt", prefix is "foo/". returns "bar.txt".
                // correct.
                VFSGlobal.Manager?.Delete(Path.Combine(path, file));
            }
            
            foreach (var dir in GetDirectories(path))
            {
                Delete(Path.Combine(path, dir), true);
            }

            VFSGlobal.Manager?.Delete(path);
        }

        public static void Move(string sourceDirName, string destDirName)
        {
            if (VFSGlobal.Manager == null) throw new IOException("VFS not initialized");
            
            // 1. Create dest
            CreateDirectory(destDirName);
            
            // 2. Move files
            foreach (var file in GetFiles(sourceDirName))
            {
                 var srcFile = Path.Combine(sourceDirName, file);
                 var dstFile = Path.Combine(destDirName, file);
                 File.Move(srcFile, dstFile);
            }
            
            // 3. Move subdirs
             foreach (var dir in GetDirectories(sourceDirName))
            {
                 var srcSubDir = Path.Combine(sourceDirName, dir);
                 var dstSubDir = Path.Combine(destDirName, dir);
                 Move(srcSubDir, dstSubDir);
            }

            // 4. Delete src
            VFSGlobal.Manager.Delete(sourceDirName);
        }
    }
}
