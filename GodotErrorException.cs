using System;
using Godot;


namespace DitoDisco.GodotUtils {

    /// <summary>
    /// Can be thrown when the result of an operation isn't <see cref="Error.Ok"/>.
    /// </summary>
    public class GodotErrorException : Exception {

        public Error Error { get; set; }

        public override string Message => $"A Godot.Error has occurred: {Error}";

        /// <param name="error">May not be <see cref="Error.Ok"/>.</param>
        public GodotErrorException(Error error) {
            if(error == Error.Ok) throw new ArgumentOutOfRangeException(nameof(error), $"'{nameof(error)}' must not be '{Error.Ok}'.");
            this.Error = error;
        }


        /// <summary>
        /// Throws a <see cref="GodotErrorException"/> when the argument is not <see cref="Error.Ok"/>, otherwise does nothing.
        /// </summary>
        public static void ThrowIfNotOk(Error err) {
            if(err != Error.Ok) {
                throw new GodotErrorException(err);
            }
        }

    }

}
