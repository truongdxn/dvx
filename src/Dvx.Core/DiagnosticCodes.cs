namespace Dvx.Core;

/// <summary>Diagnostic codes emitted by Dvx.Core and the CLI. One place so codes never collide.</summary>
public static class DiagnosticCodes
{
    /// <summary>Usage or argument parse error (exit 2).</summary>
    public const string Usage = "DVX090";

    /// <summary>A referenced assembly or type could not be resolved or loaded.</summary>
    public const string AssemblyUnreadable = "DVX180";

    /// <summary>The project could not be built or resolved.</summary>
    public const string BuildFailed = "DVX181";

    /// <summary>An SDK attribute has an unexpected shape.</summary>
    public const string AttributeInvalid = "DVX182";

    /// <summary>The Dvx SDK marker was not found in the assembly.</summary>
    public const string SdkNotFound = "DVX183";

    /// <summary>A registration has no valid identity (e.g. a step without a primary entity).</summary>
    public const string IdentityInvalid = "DVX184";

    /// <summary>Duplicate component IDs or another internal error (exit 9).</summary>
    public const string Internal = "DVX190";
}
