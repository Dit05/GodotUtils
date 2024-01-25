using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using GodotModeFlags=Godot.FileAccess.ModeFlags;


namespace DitoDisco.GodotUtils {

    public static class GodotFS {

        /// <summary>
        /// Throws a <see cref="FileNotFoundException"/> when <see cref="Godot.FileAccess.FileExists(string)"/> says <paramref name="path"/> doesn't exist.
        /// </summary>
        static void EnsureFileExists(string path) {
            if(!Godot.FileAccess.FileExists(path))
                throw new FileNotFoundException("File not found.", path);
        }

        /// <summary>
        /// Parses a .tres file to determine what kind of resource it is, and returns it as a string, ie: Material, World3D, FontFile, or Resource for custom types. Returns null on failure, like when the file is not a resource.
        /// </summary>
        public static string? DetermineResourceTypeName(string path) {
            EnsureFileExists(path);

            // Yay, file parsing time!
            string? line;

            // Get the first non-whitespace line
            using(var reader = new StreamReader(new GodotFileStream(path, GodotModeFlags.Read))) {
                // FIXME safety
                while(true) {
                    int what = reader.Read();
                    if(what == -1) return null;
                    char ch = (char)what;

                    if(char.IsWhiteSpace(ch)) continue;
                    else if(ch != '[') return null;
                    else break;
                }

                line = reader.ReadLine();
                if(line == null) return null;
            }


            int charsLeft = line.LastIndexOf(']');

            int parseIndex = 0;

            bool advance(ref int index) {
                if(charsLeft <= 0) return false;
                charsLeft--;

                index++;
                return true;
            }

            const string PREAMBLE = "gd_resource ";
            for(int i = 0; i < PREAMBLE.Length; i++) {
                if(!advance(ref parseIndex) || line[parseIndex] != PREAMBLE[i]) return null;
            }

            bool next_kvp(out string key, out string value) {
                // Parse index at start of kvp

                int equalsI = parseIndex;
                while(true) {
                    if(!advance(ref equalsI)) goto fail;
                    if(line[equalsI] == '=') break;
                }

                int startQuoteI = equalsI;
                if(!advance(ref startQuoteI) || line[startQuoteI] != '"') goto fail;

                int endQuoteI = startQuoteI;
                bool escaping = false;
                bool done = false;
                while(!done) {
                    if(!advance(ref endQuoteI)) goto fail;

                    if(!escaping) {
                        char ch = line[endQuoteI];

                        if(ch == '"') {
                            break;
                        } else if(ch == '\\') {
                            escaping = true;
                        }
                    } else {
                        escaping = false;
                    }
                }

                key = line.Substring(parseIndex, equalsI - parseIndex);
                value = line.Substring(startQuoteI + 1, endQuoteI - startQuoteI - 1);

                return true;
            fail:
                key = string.Empty;
                value = string.Empty;
                return false;
            }


            // Go through the key-value pairs (key=value key=value key=value)
            while(next_kvp(out string key, out string value)) {
                if(key == "type") return value;
            }

            // None of the keys were "type".
            return null;
        }


        private static readonly System.Reflection.Assembly _resourcesAssembly = typeof(Resource).Assembly;

        /// <summary>
        /// Determines what kind kind of <see cref="Resource"/> a .tres file is. Returns null if the file is not a resource, and just Resource for custom resources.
        /// </summary>
        public static Type? DetermineResourceType(string path) {
            string? typeName = DetermineResourceTypeName(path);
            if(typeName == null)
                return null;

            Type? type = _resourcesAssembly.GetType($"{nameof(GodotSharp)}.{typeName}");
            if(typeof(Resource).IsAssignableFrom(type)) {
                return type;
            } else {
                return null;
            }
        }

        static string Slashify(string path) => path.EndsWith("/") ? path : path + '/';

        /// <summary>
        /// Produces the names of all the entries in the directory <paramref name="path"/>.
        /// </summary>
        public static IEnumerable<string> EnumerateDirectoryEntries(string path, bool includeHidden = true) {
            DirAccess dir = DirAccess.Open(path);
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
        /// Produces the paths of all the files in the directory <paramref name="startPath"/> and its subdirectories. Moves breadth-first to save on directory changes (they're calls to Godot).
        /// </summary>
        public static IEnumerable<string> RecursivelyEnumerateFilePaths(string startPath, bool includeHidden = true) {
            DirAccess dir = DirAccess.Open(startPath);
            if(dir is null) {
                throw new Exception(DirAccess.GetOpenError().ToString());
            }

            dir.IncludeNavigational = false;
            dir.IncludeHidden = includeHidden;

            var paths = new Queue<string>();
            GodotErrorException.ThrowIfNotOk(dir.ChangeDir(startPath));
            paths.Enqueue(dir.GetCurrentDir());

            while(paths.Count > 0) {
                string path = paths.Dequeue();

                GodotErrorException.ThrowIfNotOk(dir.ChangeDir(path));

                dir.ListDirBegin();

                while(true) {
                    string entry = dir.GetNext();
                    if(entry.Length == 0)
                        break;

                    string fullEntry = Slashify(path) + entry;
                    if(dir.CurrentIsDir()) {
                        paths.Enqueue(fullEntry);
                    } else {
                        yield return fullEntry;
                    }
                }

                dir.ListDirEnd();
            }
            dir.Dispose();
        }

    }

}
