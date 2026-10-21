using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dvx.Cli.Commands;
using Dvx.Core.Logging;

namespace Dvx.Cli;

public static class Program
{
    private static int Main(string[] args) =>
        Run(args, Console.Out, Console.Error, Directory.GetCurrentDirectory());

    /// <summary>Testable entry point: parses arguments, runs the command, logs it and returns the exit code.</summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, string workingDirectory)
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = new CommandOutcome();
        string command = "dvx";
        int exitCode = ExitCodes.Internal;
        bool json = args.Contains("--json", StringComparer.Ordinal);

        try
        {
            var jsonOption = new Option<bool>("--json") { Description = "Write the result as JSON on stdout.", Recursive = true };
            var root = new RootCommand("dvx: Dataverse plugin tooling");
            root.Options.Add(jsonOption);
            root.Subcommands.Add(DescribeCommand.Create(jsonOption, stdout, stderr, workingDirectory, outcome));

            ParseResult parseResult = root.Parse(args);
            command = parseResult.CommandResult.Command.Name;

            if (parseResult.Errors.Count > 0)
            {
                var diagnostics = parseResult.Errors.Select(e => Output.Error("DVX001", e.Message)).ToArray();
                outcome.DiagnosticCodes.Add("DVX001");
                Output.WriteError(stdout, stderr, json, diagnostics);
                exitCode = ExitCodes.Usage;
            }
            else
            {
                exitCode = parseResult.Invoke(new InvocationConfiguration { Output = stdout, Error = stderr });
            }
        }
        catch (Exception ex)
        {
            outcome.DiagnosticCodes.Add("DVX900");
            Output.WriteError(stdout, stderr, json, new[] { Output.Error("DVX900", "Internal error: " + ex.Message) });
            exitCode = ExitCodes.Internal;
        }
        finally
        {
            try
            {
                string root = outcome.RepoRoot ?? CommandLog.FindRepoRoot(workingDirectory) ?? Path.GetFullPath(workingDirectory);
                // Option names only (tokens that start with "--"); values are never recorded.
                var optionNames = args.Where(a => a.StartsWith("--", StringComparison.Ordinal)).Select(a => a.Split('=')[0]).ToArray();
                new CommandLog().Append(root, command, optionNames, exitCode, outcome.DiagnosticCodes, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                // A logging failure must never change the exit code.
                stderr.Write("dvx: could not write the command log: " + ex.Message + "\n");
            }
        }

        return exitCode;
    }
}
