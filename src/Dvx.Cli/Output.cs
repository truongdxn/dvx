using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dvx.Contracts;
using Dvx.Core.Output;

namespace Dvx.Cli;

/// <summary>Writes command results: JSON (<c>--json</c>) or a short text form.</summary>
internal static class Output
{
    public static void WriteDescribeText(TextWriter stdout, DescribeResult result)
    {
        int kindWidth = result.Components.Select(c => c.Kind.Length).DefaultIfEmpty(0).Max();
        int idWidth = result.Components.Select(c => c.Id.Length).DefaultIfEmpty(0).Max();
        foreach (ComponentDto c in result.Components)
        {
            stdout.Write(c.Kind.PadRight(kindWidth) + "  " + c.Id.PadRight(idWidth) + "  " + c.Key + "\n");
        }
    }

    public static void WriteJson<T>(TextWriter stdout, T value) => stdout.Write(JsonOutput.Serialize(value));

    /// <summary>Failure output: ErrorResult on stdout with <c>--json</c>, otherwise one line per diagnostic on stderr.</summary>
    public static void WriteError(TextWriter stdout, TextWriter stderr, bool json, IReadOnlyList<DiagnosticDto> diagnostics)
    {
        if (json)
        {
            WriteJson(stdout, new ErrorResult { Diagnostics = diagnostics });
            return;
        }

        foreach (DiagnosticDto d in diagnostics)
        {
            stderr.Write(d.Code + ": " + d.Message + "\n");
        }
    }

    public static DiagnosticDto Error(string code, string message) => new() { Code = code, Severity = "error", Message = message };
}
