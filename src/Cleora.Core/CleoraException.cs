using System;

namespace Cleora.Core
{
    /// <summary>
    /// A failure the user should hear about. <see cref="Code"/> is a short stable key that the app maps to a
    /// Vietnamese sentence; the message is for logs only.
    /// </summary>
    public sealed class CleoraException : Exception
    {
        public CleoraException(string code, string message, Exception? inner = null)
            : base(message, inner)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
