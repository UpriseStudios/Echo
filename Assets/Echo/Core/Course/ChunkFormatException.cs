using System;

namespace Echo.Core.Courses
{
    public sealed class ChunkFormatException : Exception
    {
        public ChunkFormatException(string message) : base(message) { }
    }
}
