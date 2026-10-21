using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Dvx.Core.Reading;

/// <summary>Output DLL and compile-time reference files of a plugin project.</summary>
public sealed record ProjectOutput(string TargetPath, IReadOnlyList<string> ReferencePaths);

/// <summary>
/// Asks MSBuild (child process) for a project's TargetPath and ReferencePath items.
/// <c>build: true</c> runs the Build target; <c>build: false</c> runs only ResolveAssemblyReferences
/// (no compile; the project must be restored and its output must already exist to be readable).
/// </summary>
public static class ProjectOutputResolver
{
    public static ProjectOutput Resolve(string projectPath, bool build)
    {
        string target = build ? "Build" : "ResolveAssemblyReferences";
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";

        var startInfo = new ProcessStartInfo(dotnet)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-t:" + target);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-getProperty:TargetPath");
        startInfo.ArgumentList.Add("-getItem:ReferencePath");

        string stdout;
        string stderr;
        int exitCode;
        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new DvxReadException("DVX181", "Could not start '" + dotnet + "'.");
            // Read stderr asynchronously so a full pipe cannot block the child.
            var stderrTask = process.StandardError.ReadToEndAsync();
            stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            stderr = stderrTask.GetAwaiter().GetResult();
            exitCode = process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new DvxReadException("DVX181", "Could not start '" + dotnet + "': " + ex.Message);
        }

        if (exitCode != 0)
        {
            throw new DvxReadException("DVX181",
                "MSBuild failed for '" + projectPath + "' (exit code " + exitCode + "): " + Summarize(stdout, stderr));
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(stdout);
            string? targetPath = doc.RootElement.GetProperty("Properties").GetProperty("TargetPath").GetString();
            if (string.IsNullOrEmpty(targetPath))
            {
                throw new DvxReadException("DVX181", "MSBuild returned no TargetPath for '" + projectPath + "'.");
            }

            var references = new List<string>();
            foreach (JsonElement item in doc.RootElement.GetProperty("Items").GetProperty("ReferencePath").EnumerateArray())
            {
                string? identity = item.GetProperty("Identity").GetString();
                if (!string.IsNullOrEmpty(identity)) references.Add(identity);
            }

            return new ProjectOutput(targetPath, references);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new DvxReadException("DVX181", "Unexpected MSBuild output for '" + projectPath + "': " + ex.Message);
        }
    }

    private static string Summarize(string stdout, string stderr)
    {
        // MSBuild prints diagnostics as "error ..." lines on stdout; keep those, else the tail of all output.
        string all = stdout + Environment.NewLine + stderr;
        string[] lines = all.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] errors = lines.Where(l => l.Contains(": error ", StringComparison.Ordinal)).Distinct().Take(10).ToArray();
        string[] chosen = errors.Length > 0 ? errors : lines.TakeLast(10).ToArray();
        return string.Join(" | ", chosen);
    }
}
