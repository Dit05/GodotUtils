using System;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using Godot;


namespace DitoDisco.GodotUtils {

    [Obsolete]
    public static class ResourceParser {

        private static readonly System.Reflection.Assembly resourcesAssembly = typeof(Resource).Assembly;



        /// <summary>
        /// Extracts the key-value pairs from the part of a .tres file that goes [gd_resource (...)].
        /// </summary>
        public static IEnumerable<KeyValuePair<string, string>> ParseResourceHeader(TextReader reader) => InternalParseResourceHeader(reader, null);

        /// <summary>
        /// Opens the file at <paramref name="path"/> via <see cref="GodotFS"/>, then passes it to <see cref="ParseResourceHeader(TextReader)"/>.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, string>> ParseResourceHeader(string path) {
            GodotFS.AssertFileExists(path);

            using(var reader = new StreamReader(new GodotFileStream(path, Godot.FileAccess.ModeFlags.Read))) {
                return InternalParseResourceHeader(reader, path);
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> InternalParseResourceHeader(TextReader reader, string? exceptionPath) {
            [DoesNotReturn]
            void die(string msg) => throw new ResourceParseException(msg, exceptionPath);

            // Yay, file parsing time!
            string? line;

            // Get the first non-whitespace line
            {
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

        /// <summary>
        /// Attempts to determine what kind of <see cref="Resource"/> a .tres file is. Returns <see cref="Resource"/> for custom resources.
        /// </summary>
        [RequiresUnreferencedCode("This method uses reflection to find Godot resource types.")]
        public static Type GuessResourceType(string path) {
            string typeName = GuessResourceTypeName(path);

            Type? type = resourcesAssembly.GetType($"{nameof(Godot)}.{typeName}");
            if(typeof(Resource).IsAssignableFrom(type)) {
                return type;
            } else {
                throw new ResourceParseException($"There's no type named '{typeName}' in the assembly '{resourcesAssembly.GetName()}'.", path);
            }
        }

    }

}
