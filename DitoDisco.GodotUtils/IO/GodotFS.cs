using Godot;
using System;
using System.IO;
using System.Collections.Generic;


namespace DitoDisco.GodotUtils {

    public static class GodotFS {

        /// <summary>
        /// Throws a <see cref="FileNotFoundException"/> when <see cref="Godot.FileAccess.FileExists(string)"/> says <paramref name="path"/> doesn't exist.
        /// </summary>
        public static void AssertFileExists(string path) {
            if(!Godot.FileAccess.FileExists(path)) {
                throw new FileNotFoundException("File did not exist.", path);
            }
        }

        [Obsolete($"This method has been moved to {nameof(ResourceParser)}. It will be removed from this type in future versions.")]
        public static IEnumerable<KeyValuePair<string, string>> ParseResourceHeader(string path) {
            return ResourceParser.ParseResourceHeader(path);
        }


        [Obsolete($"This method has been moved to {nameof(ResourceParser)}. It will be removed from this type in future versions.")]
        public static string GuessResourceTypeName(string path) {
            return ResourceParser.GuessResourceTypeName(path);
        }

        [Obsolete($"This method has been moved to {nameof(ResourceParser)}. It will be removed from this type in future versions.")]
        public static Type GuessResourceType(string path) {
            return ResourceParser.GuessResourceType(path);
        }



        static string Slashify(string path) => path.EndsWith("/") ? path : path + '/';

        /// <summary>
        /// Produces the names of all the entries in the directory <paramref name="path"/>.
        /// </summary>
        public static IEnumerable<string> EnumerateDirectoryEntries(string path, bool includeHidden = true) {
            DirAccess? dir = DirAccess.Open(path);
            if(dir is null) {
                throw new Exception(DirAccess.GetOpenError().ToString());
            }

            dir.IncludeNavigational = false;
            dir.IncludeHidden = includeHidden;
            GodotErrorException.ThrowIfNotOk(dir.ListDirBegin());

            while(true) {
                string entry = dir.GetNext();
                if(entry == string.Empty) break;
                else yield return entry;
            }

            dir.ListDirEnd();
            dir.Dispose();
        }

        /// <summary>
        /// Produces the paths of all the files in the directory <paramref name="startPath"/> and its subdirectories. Moves breadth-first to save on directory changes (they're calls to Godot).
        /// </summary>
        public static IEnumerable<string> RecursivelyEnumerateFilePaths(string startPath, bool includeHidden = true) {
            DirAccess? dir = DirAccess.Open(startPath);
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
