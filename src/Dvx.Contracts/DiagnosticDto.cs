using System.Collections.Generic;

namespace Dvx.Contracts
{
    /// <summary>Diagnostic in the schema shared by all commands (SPEC §10).</summary>
    public sealed class DiagnosticDto
    {
        public string Code { get; set; } = "";

        public string Severity { get; set; } = "error";

        public string Message { get; set; } = "";

        public string? File { get; set; }

        public int? Line { get; set; }
    }

    /// <summary>Stdout payload of a failed command when <c>--json</c> is set.</summary>
    public sealed class ErrorResult
    {
        public IReadOnlyList<DiagnosticDto> Diagnostics { get; set; } = new DiagnosticDto[0];
    }
}
