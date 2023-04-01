using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using GodotModeFlags=global::Godot.FileAccess.ModeFlags;
using FileAccess=global::Godot.FileAccess;
using DirAccess=global::Godot.DirAccess;

namespace DitoDisco.Godot {

    /// <summary>
    /// Wraps <see cref="FileAccess"/> as a <see cref="System.IO.Stream"/>.
    /// </summary>
    public partial class GodotFileStream : System.IO.Stream {

        private FileAccess _file;
        private GodotModeFlags _flags;

        public GodotFileStream(string path, GodotModeFlags flags) {
            _file = FileAccess.Open(path, flags);
            _flags = flags;

            if(_file is null)
                throw new GodotErrorException(FileAccess.GetOpenError());
        }

        public override void Flush() {
            _file.Flush();
        }

        protected override void Dispose(bool disposing) {
            _file.Dispose();
        }


        public override bool CanRead => _flags == GodotModeFlags.Read || _flags == GodotModeFlags.ReadWrite || _flags == GodotModeFlags.WriteRead;
        public override bool CanWrite => _flags == GodotModeFlags.Write || _flags == GodotModeFlags.ReadWrite || _flags == GodotModeFlags.WriteRead;
        public override bool CanSeek => true;

        public override long Position {
            get => (long)_file.GetPosition();
            set => Seek(value, SeekOrigin.Begin);
        }

        public override long Length {
            get => (long)_file.GetLength();
        }


        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            switch(origin) {
                case SeekOrigin.Begin:
                    _file.Seek((ulong)offset);
                    break;
                case SeekOrigin.Current:
                    _file.Seek((ulong)_file.GetPosition() + (ulong)offset);
                    break;
                case SeekOrigin.End:
                    _file.SeekEnd(offset);
                    break;
            }

            return (long)_file.GetPosition();
        }


        public override int ReadByte() {
            if(_file.GetPosition() >= _file.GetLength())
                return -1;
            else
                return _file.Get8();
        }
        public override int Read(byte[] buffer, int offset, int count) {
            if(offset + count > buffer.Length)
                throw new ArgumentException("offset + count is greater than the buffer's length.");
            if(buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if(offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();
            if(CanRead == false)
                throw new NotSupportedException();

            for(int i = 0; i < count; i++) {
                int read = ReadByte();
                if(read == -1)
                    return i;
                buffer[offset + i] = (byte)read;
            }

            return count;
        }

        public override void WriteByte(byte b) {
            _file.Store8(b);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            if(offset + count > buffer.Length)
                throw new ArgumentException("offset + count is greater than the buffer's length.");
            if(buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if(offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();
            if(CanWrite == false)
                throw new NotSupportedException();

            for(int i = offset; i < offset + count; i++)
                _file.Store8(buffer[i]);
        }

    }


    public static class GodotFS {

        //private static Godot.Directory _existenceChecker; // Yay, no need for this anymore!

        public static GodotFileStream Open(string path, GodotModeFlags flags) {
            return new GodotFileStream(path, flags);
        }

        /// <summary>
        /// Checks whether a file exists or not. <paramref name="path"/> can be absolute, or relative to res://.
        /// </summary>
        public static bool FileExists(string path) => FileAccess.FileExists(path);

        static void EnsureFileExists(string path) {
            if(!FileExists(path))
                throw new FileNotFoundException("File not found.", path);
        }

        /// <summary>
        /// Determines what kind kind of resource a .tres file is, and returns it as a string, ie: Material, World3D, FontFile, or Resource for custom types, or null if the file is not a resource.
        /// <param name="ignoreExtension">If set, files are assumed to be resources even if their extension isn't '.tres'.</param>
        /// </summary>
        public static string? DetermineResourceTypeName(string path, bool ignoreExtension = false) {
            EnsureFileExists(path);
            if(!ignoreExtension && !path.EndsWith(".tres"))
                return null;

            // File parsing time!!!
            string? descriptor;

            // Get the first non-whitespace line
            using(var reader = new StreamReader(Open(path, GodotModeFlags.Read))) {
                while(true) {
                    descriptor = reader.ReadLine();
                    if(descriptor == null)
                        return null;
                    else if(!string.IsNullOrWhiteSpace(descriptor))
                        break;
                }
            }


            // Trim before and after the []
            int lBraceI = descriptor.IndexOf('[');
            int rBraceI = descriptor.IndexOf(']');

            if(lBraceI == -1 || rBraceI == -1 || rBraceI <= lBraceI)
                return null;

            descriptor = descriptor.Remove(rBraceI).Remove(0, lBraceI + 1);
            string[] kvps = descriptor.Split(' ');

            if(kvps.Length == 0 || kvps[0] != "gd_resource")
                return null;

            // Go through the key-value pairs (key=value key=value key=value)
            for(int i = 1; i < kvps.Length; i++) {
                string[] parts = kvps[i].Split('=');
                if(parts.Length != 2)
                    continue;

                if(parts[0] != "type")
                    continue;

                string typeName = parts[1];
                if(!typeName.StartsWith("\"") || !typeName.EndsWith("\""))
                    continue;

                // WE FOUND IT!!
                return typeName.Trim('"');
            }

            // None of the pairs were any good
            return null;
        }

        private static readonly Assembly _resourcesAssembly = typeof(Resource).Assembly;

        /// <summary>
        /// Determines what kind kind of resource a .tres file is. Returns null if the file is not a resource, and just Resource for custom resources.
        /// <param name="ignoreExtension">If set, files are assumed to be resources even if their extension isn't '.tres'.</param>
        /// </summary>
        public static Type? DetermineResourceType(string path, bool ignoreExtension = false) {
            string? typeName = DetermineResourceTypeName(path, ignoreExtension);
            if(typeName == null)
                return null;

            Type? type = _resourcesAssembly.GetType($"Godot.{typeName}");
            if(typeof(Resource).IsAssignableFrom(type)) {
                return type;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Checks whether a directory exists or not. <paramref name="path"/> can be absolute, or relative to res://.
        /// </summary>
        public static bool DirectoryExists(string path) => DirAccess.DirExistsAbsolute(path);

        static string Slashify(string path) => path.EndsWith("/") ? path : path + '/';

        /// <summary>
        /// Produces the names of all the entries in the directory <paramref name="path"/>.
        /// </summary>
        public static IEnumerable<string> EnumerateDirectoryEntries(string path, bool includeHidden = true) {
            var dir = DirAccess.Open(path);
            if(dir is null) {
                throw new Exception(DirAccess.GetOpenError().ToString());
            }

            dir.IncludeNavigational = false;
            dir.IncludeHidden = includeHidden;
            dir.ListDirBegin();

            while(true) {
                string entry = dir.GetNext();
                if(entry == string.Empty)
                    break;

                yield return entry;
            }

            dir.ListDirEnd();
            dir.Dispose();
        }

        /// <summary>
        /// Produces the paths of all the files in the directory <paramref name="startPath"/> and its subdirectories.
        /// </summary>
        public static IEnumerable<string> RecursivelyEnumerateFilePaths(string startPath, bool includeHidden = true) {
            var dir = DirAccess.Open(startPath);
            if(dir is null) {
                throw new Exception(DirAccess.GetOpenError().ToString());
            }
            dir.IncludeNavigational = false;
            dir.IncludeHidden = includeHidden;

            var paths = new Queue<string>();
            paths.Enqueue(startPath);

            while(paths.Count > 0) {
                string path = Slashify(paths.Dequeue());

                GodotErrorException.ThrowIfNotOk(dir.ChangeDir(path));

                dir.ListDirBegin();

                while(true) {
                    string entry = dir.GetNext();
                    if(entry == string.Empty)
                        break;

                    if(DirectoryExists(entry)) {
                        paths.Enqueue(entry);
                    } else {
                        yield return path + entry;
                    }
                }

                dir.ListDirEnd();
                dir.Dispose();
            }
        }


        /// <summary>
        /// Returns an array containing the paths of all the entries (files and directories) in the directory <paramref name="path"/>.
        /// </summary>
        public static string[] GetDirectoryEntries(string path, bool includeHidden = false) {
            var entries = new List<string>();
            foreach(string entry in EnumerateDirectoryEntries(path, includeHidden))
                entries.Add(entry);
            return entries.ToArray();
        }

    }

}
