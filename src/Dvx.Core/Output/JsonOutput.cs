using System.Text.Encodings.Web;
using System.Text.Json;

namespace Dvx.Core.Output;

/// <summary>Deterministic JSON for command results: camelCase, indented, "\n" line endings, trailing newline.</summary>
public static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options).Replace("\r\n", "\n", System.StringComparison.Ordinal) + "\n";
}
