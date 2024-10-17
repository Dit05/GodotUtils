using System;


namespace DitoDisco.GodotUtils {

    /// <summary>
    /// The exception that is thrown when <see cref="GodotFS"/> can't parse a serialized resource.
    /// </summary>
    public class ResourceParseException : Exception {

        /// <summary>Path to the resource.</summary>
        public string? Path { get; private set; }


        public ResourceParseException(string message, string? path = null) : base(message) {
            Path = path;
        }

    }

}
