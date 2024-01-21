using System;
using System.IO;
using GodotFileAccess=Godot.FileAccess;
using GodotModeFlags=global::Godot.FileAccess.ModeFlags;


namespace DitoDisco.GodotUtils {

    /// <summary>
    /// Wraps <see cref="GodotFileAccess"/> as a <see cref="System.IO.Stream"/>.
    /// </summary>
    public partial class GodotFileStream : System.IO.Stream {

        private bool disposed = false;
        private GodotFileAccess file;
        private readonly GodotModeFlags flags;



        public GodotFileStream(GodotFileAccess file, GodotModeFlags flags) {
            this.file = file;
            this.flags = flags;
        }

        public GodotFileStream(string path, GodotModeFlags flags) {
            file = GodotFileAccess.Open(path, flags);
            this.flags = flags;

            if(file is null) throw new GodotErrorException(GodotFileAccess.GetOpenError());
        }



        public override void Flush() {
            EnsureNotDisposed();
            file.Flush();
        }


        private void EnsureNotDisposed() {
            if(disposed) throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        /// For the purpose of going into expressions.
        /// </summary>
        private bool EnsureNotDisposedFalse() {
            EnsureNotDisposed();
            return false;
        }

        protected override void Dispose(bool disposing) {
            if(disposed) return;

            if(disposing) {
                file.Dispose();
            }

            file = null!;

            disposed = true;
        }


        public override bool CanRead => EnsureNotDisposedFalse() || flags == GodotModeFlags.Read || flags == GodotModeFlags.ReadWrite || flags == GodotModeFlags.WriteRead;
        public override bool CanWrite => EnsureNotDisposedFalse() || flags == GodotModeFlags.Write || flags == GodotModeFlags.ReadWrite || flags == GodotModeFlags.WriteRead;
        public override bool CanSeek => !EnsureNotDisposedFalse();

        public override long Position {
            get {
                EnsureNotDisposed();
                return (long)file.GetPosition();
            }
            set {
                EnsureNotDisposed();
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override long Length {
            get {
                EnsureNotDisposed();
                return (long)file.GetLength();
            }
        }


        public override void SetLength(long value) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) {
            EnsureNotDisposed();

            switch(origin) {
                case SeekOrigin.Begin:
                    file.Seek((ulong)offset);
                    break;
                case SeekOrigin.Current:
                    file.Seek((ulong)((long)file.GetPosition() + offset));
                    break;
                case SeekOrigin.End:
                    file.SeekEnd(offset);
                    break;
            }

            return Position;
        }


        public override int ReadByte() {
            EnsureNotDisposed();

            if(file.GetPosition() >= file.GetLength()) {
                return -1;
            } else {
                return file.Get8();
            }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            EnsureNotDisposed();

            if(offset + count > buffer.Length) throw new ArgumentException("offset + count is greater than the buffer's length.");
            if(buffer == null) throw new ArgumentNullException(nameof(buffer));
            if(offset < 0 || count < 0) throw new ArgumentOutOfRangeException();
            if(CanRead == false) throw new NotSupportedException();

            for(int i = 0; i < count; i++) {
                int read = ReadByte();
                if(read == -1)
                    return i;
                buffer[offset + i] = (byte)read;
            }

            return count;
        }

        public override void WriteByte(byte b) {
            EnsureNotDisposed();

            file.Store8(b);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            EnsureNotDisposed();

            if(offset + count > buffer.Length) throw new ArgumentException("offset + count is greater than the buffer's length.");
            if(buffer == null) throw new ArgumentNullException(nameof(buffer));
            if(offset < 0 || count < 0) throw new ArgumentOutOfRangeException();
            if(CanWrite == false) throw new NotSupportedException();

            for(int i = offset; i < offset + count; i++) {
                file.Store8(buffer[i]);
            }
        }

    }

}
