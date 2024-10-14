using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using GodotModeFlags=Godot.FileAccess.ModeFlags;


namespace DitoDisco.GodotUtils {

    public static class GodotFS {

        /// <summary>
        /// Throws a <see cref="FileNotFoundException"/> when <see cref="Godot.FileAccess.FileExists(string)"/> says <paramref name="path"/> doesn't exist.
        /// </summary>
        static void EnsureFileExists(string path) {
            if(!Godot.FileAccess.FileExists(path)) {
                throw new FileNotFoundException("File did not exist.", path);
            }
        }

        /// <summary>
        /// Extracts the key-value pairs from the part of a .tres file that goes [gd_resource (...)].
        /// </summary>
        public static IEnumerable<KeyValuePair<string, string>> ParseResourceHeader(string path) {
            [DoesNotReturn]
            void die(string msg) => throw new ResourceParseException(msg, path);

            EnsureFileExists(path);

            // Yay, file parsing time!
            string? line;

            // Get the first non-whitespace line
            using(var reader = new StreamReader(new GodotFileStream(path, GodotModeFlags.Read))) {
                int safety1 = 10_000;
                while(safety1-- > 0) {
                    int what = reader.Read();
                    if(what == -1) die("Reached EoF without ever finding anything other than whitespace.");
                    char ch = (char)what;

                    if(char.IsWhiteSpace(ch)) continue;
                    else if(ch != '[') die("Expected to find '[' as the first non-whitespace character.");
                    else break;
                }

                if(safety1 <= 0) {
                    die("File starts with an excessive amount of whitespace.");
                }

                line = reader.ReadLine();
                if(line == null) die("Expected more characters after the '['.");
            }


            int charsLeft = line.LastIndexOf(']');
            int parseIndex = 0;

            void expect(string str) {
                void _die() => die($"Expected \"{str}\" at index {parseIndex} of the first non-whitespace line.");
                if(charsLeft < str.Length) _die();

                for(int i = 0; i < str.Length; i++) {
                    if(line[parseIndex] != str[i]) _die();
                    parseIndex++;
                }
                charsLeft--;
            }

            void eat_whitespace() {
                while(charsLeft > 0 && char.IsWhiteSpace(line[parseIndex])) {
                    parseIndex++;
                    charsLeft--;
                }
            }

            int next_ch() {
                if(charsLeft > 0) {
                    charsLeft--;
                    char ch = line[parseIndex];
                    parseIndex++;
                    return ch;
                } else {
                    return -1;
                }
            }

            string cut(int start, int untilExcl) {
                return line.Substring(startIndex: start, length: untilExcl - start);
            }

            expect("gd_resource");
            eat_whitespace();

            // Stupid state machine
            const int STATE_KEY = 1;
            const int STATE_VALUE = 2;
            const int STATE_VALUE_ESCAPING = 3;

            int state = STATE_KEY;
            int keyStart = parseIndex;
            int keyEnd = -1;
            int valueStart = -1;

            int safety2 = 1_000_000;
            while(safety2-- > 0) {
                switch(state) {

                    case STATE_KEY:{
                        int ch = next_ch();
                        if(ch == -1) die("Expected '=' after key.");

                        char cch = (char)ch;
                        bool over = (cch == '=' || char.IsWhiteSpace(cch));

                        if(over) {
                            keyEnd = parseIndex;

                            eat_whitespace();
                            expect("=");
                            eat_whitespace();
                            expect("\"");
                            valueStart = parseIndex;
                            state = STATE_VALUE;
                        }
                    }break;

                    case STATE_VALUE:{
                        int ch = next_ch();
                        if(ch == -1) die("Expected '\"' to close a value.");

                        char cch = (char)ch;
                        // The .tres format better not have escaping more complicated than this.
                        if(cch == '\\') {
                            state = STATE_VALUE_ESCAPING;
                            break;
                        }

                        if(cch == '"') {
                            yield return new KeyValuePair<string, string>(cut(keyStart, keyEnd), cut(valueStart, parseIndex));
                            next_ch();
                            eat_whitespace();

                            if(cch == ']') {
                                yield break; // Done
                            }
                            keyStart = parseIndex;
                            state = STATE_KEY;
                        }
                    }break;

                    case STATE_VALUE_ESCAPING:{
                        int ch = next_ch();
                        if(ch == -1) die("Expected a character to be escaped.");
                        state = STATE_VALUE;
                    }break;

                }
            }

            die("The resource tag has too much text.");
        }


        /// <summary>
        /// Attempts to parse a .tres serialized resource file to determine what kind of resource it is, and returns it as a string, ie: Material, World3D, FontFile, or Resource for custom types. Returns null on failure, like when the file is not a resource.
        /// </summary>
        public static string GuessResourceTypeName(string path) {
            foreach(KeyValuePair<string, string> kvp in ParseResourceHeader(path)) {
                if(kvp.Key == "type") {
                    return kvp.Value;
                }
            }

            throw new ResourceParseException("None of the resource header key-value pairs indicated the type of the resource.", path);
        }

        private static readonly System.Reflection.Assembly resourcesAssembly = typeof(Resource).Assembly;

        /// <summary>
        /// Attempts to determine what kind of <see cref="Resource"/> a .tres file is. Returns <see cref="Resource"/> for custom resources.
        /// </summary>
        public static Type GuessResourceType(string path) {
            string typeName = GuessResourceTypeName(path);

            Type? type = resourcesAssembly.GetType($"{nameof(Godot)}.{typeName}");
            if(typeof(Resource).IsAssignableFrom(type)) {
                return type;
            } else {
                throw new ResourceParseException($"There's no type named '{typeName}' in the assembly '{resourcesAssembly.GetName()}'.", path);
            }
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
