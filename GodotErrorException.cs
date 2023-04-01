using System;
using Godot;


namespace DitoDisco.Godot {

    /// <summary>
    /// Can be thrown when the result of an operation isn't <see cref="Error.Ok"/>.
    /// </summary>
    public class GodotErrorException : Exception {

        public Error Error { get; set; }

        public override string Message => $"A Godot.Error has occurred: {Error}";

        public GodotErrorException(Error error) {
            this.Error = error;
        }


        /// <summary>
        /// Throws a <see cref="GodotErrorException"/> when the argument expression is not <see cref="Error.Ok"/>, otherwise does nothing.
        /// </summary>
        public static void ThrowIfNotOk(Error err) {
            if(err != Error.Ok) {
                throw new GodotErrorException(err);
            }
        }

    }

}
