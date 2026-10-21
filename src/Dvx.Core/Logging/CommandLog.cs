using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Dvx.Core.Logging;

/// <summary>
/// Appends one summary line per command to <c>.dvx/logs/yyyy-MM-dd.jsonl</c> (SPEC §12).
/// Records option names only, never option values.
/// </summary>
public sealed class CommandLog
{
    private const string RepoMarker = "dvx.repo.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Nearest ancestor (including <paramref name="startDirectory"/>) holding dvx.repo.json, else null.</summary>
    public static string? FindRepoRoot(string startDirectory)
    {
        for (string? dir = Path.GetFullPath(startDirectory); dir != null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, RepoMarker))) return dir;
        }

        return null;
    }

    public void Append(
        string repoRoot,
        string command,
        IEnumerable<string> optionNames,
        int exitCode,
        IEnumerable<string> diagnosticCodes,
        TimeSpan duration)
    {
        DateTime now = DateTime.UtcNow;
        string line = JsonSerializer.Serialize(
            new
            {
                ts = now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
                command,
                options = optionNames.ToArray(),
                exitCode,
                diagnostics = diagnosticCodes.ToArray(),
                durationMs = (long)duration.TotalMilliseconds,
            },
            Options);

        string directory = Path.Combine(repoRoot, ".dvx", "logs");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".jsonl");
        File.AppendAllText(file, line + "\n");
    }
}
