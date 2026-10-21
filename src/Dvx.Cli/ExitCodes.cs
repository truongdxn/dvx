namespace Dvx.Cli;

/// <summary>Process exit codes (SPEC §12).</summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int Diagnostics = 1;
    public const int Usage = 2;
    public const int Credentials = 3;
    public const int EnvNotAllowed = 4;
    public const int EnvChanged = 5;
    public const int ConfirmationRequired = 6;
    public const int PartialAll = 7;
    public const int Dataverse = 8;
    public const int Internal = 9;
}
