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
    /// <summary>Upper bound for one MSBuild run; the process tree is killed when it is exceeded.</summary>
    internal static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(10);

    public static ProjectOutput Resolve(string projectPath, bool build) => Resolve(projectPath, build, BuildTimeout);

    internal static ProjectOutput Resolve(string projectPath, bool build, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        projectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(projectPath))
        {
            throw new DvxReadException("DVX181", "Project file '" + projectPath + "' does not exist.");
        }

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
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(timeout))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // The process ended on its own between the timeout and the kill.
                }

                throw new DvxReadException("DVX181",
                    "MSBuild for '" + projectPath + "' did not finish within " + timeout.TotalSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " seconds and was stopped.");
            }

            // Parameterless WaitForExit lets the redirected streams drain completely.
            process.WaitForExit();
            stdout = stdoutTask.GetAwaiter().GetResult();
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
