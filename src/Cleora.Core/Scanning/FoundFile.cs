using System;

namespace Cleora.Core.Scanning
{
    public sealed record FoundFile(string Path, long Length, DateTime LastWriteUtc);
}
