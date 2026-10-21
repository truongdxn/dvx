using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using Dvx.Contracts;
using Dvx.Core.Logging;
using Dvx.Core.Reading;
using Dvx.Core.State;

namespace Dvx.Cli.Commands;

/// <summary>Collects what the command log needs from a running command.</summary>
internal sealed class CommandOutcome
{
    public List<string> DiagnosticCodes { get; } = new();

    public string? RepoRoot { get; set; }
}

internal static class DescribeCommand
{
    public static Command Create(Option<bool> jsonOption, TextWriter stdout, TextWriter stderr, string workingDirectory, CommandOutcome outcome)
    {
        var project = new Option<string>("--project") { Description = "Path to the plugin .csproj.", Required = true };
        var command = new Command("describe", "Show the desired registration with identity keys and IDs.");
        command.Options.Add(project);
        command.SetAction(parseResult => Run(
            parseResult.GetValue(project)!, parseResult.GetValue(jsonOption), stdout, stderr, workingDirectory, outcome));
        return command;
    }

    private static int Run(string projectArgument, bool json, TextWriter stdout, TextWriter stderr, string workingDirectory, CommandOutcome outcome)
    {
        string projectPath = Path.GetFullPath(projectArgument, workingDirectory);
        if (!File.Exists(projectPath))
        {
            return Fail(ExitCodes.Usage, json, stdout, stderr, outcome,
                Output.Error("DVX001", "Project file '" + projectArgument + "' does not exist."));
        }

        outcome.RepoRoot = CommandLog.FindRepoRoot(workingDirectory)
            ?? CommandLog.FindRepoRoot(Path.GetDirectoryName(projectPath)!);

        try
        {
            stderr.Write("Building " + Path.GetFileName(projectPath) + "...\n");
            DescribeResult result = DescribeService.Describe(projectPath);
            if (json) Output.WriteJson(stdout, result);
            else Output.WriteDescribeText(stdout, result);
            return ExitCodes.Success;
        }
        catch (DvxReadException ex)
        {
            return Fail(ExitCodes.Diagnostics, json, stdout, stderr, outcome, Output.Error(ex.Code, ex.Message));
        }
        catch (DuplicateComponentIdException ex)
        {
            return Fail(ExitCodes.Internal, json, stdout, stderr, outcome, Output.Error("DVX900", ex.Message));
        }
    }

    internal static int Fail(int exitCode, bool json, TextWriter stdout, TextWriter stderr, CommandOutcome outcome, DiagnosticDto diagnostic)
    {
        outcome.DiagnosticCodes.Add(diagnostic.Code);
        Output.WriteError(stdout, stderr, json, new[] { diagnostic });
        return exitCode;
    }
}
