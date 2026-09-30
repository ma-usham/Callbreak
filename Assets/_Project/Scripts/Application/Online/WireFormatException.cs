using System;

namespace Callbreak.Application.Online
{
    /// <summary>A message from the network could not be read. The receiver drops it; it never crashes a table.</summary>
    public sealed class WireFormatException : Exception
    {
        public WireFormatException(string message)
            : base(message)
        {
        }

        public WireFormatException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
