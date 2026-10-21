using System;

namespace Dvx.Core.Reading;

/// <summary>A plugin assembly or project could not be read. <see cref="Code"/> is a DVX diagnostic code.</summary>
public sealed class DvxReadException : Exception
{
    public DvxReadException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
