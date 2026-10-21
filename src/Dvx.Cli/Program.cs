using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dvx.Cli.Commands;
using Dvx.Contracts;
using Dvx.Core;
using Dvx.Core.Logging;
using Dvx.Core.State;

namespace Dvx.Cli;

public static class Program
{
    private static int Main(string[] args) =>
        Run(args, Console.Out, Console.Error, Directory.GetCurrentDirectory());

    /// <summary>Testable entry point: parses arguments, runs the command, logs it and returns the exit code.</summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, string workingDirectory) =>
        Run(args, stdout, stderr, workingDirectory, DescribeService.Describe);

    /// <summary>Test seam: the describe implementation can be replaced to force unexpected failures.</summary>
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr, string workingDirectory, Func<string, DescribeResult> describe)
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = new CommandOutcome();
        string command = "dvx";
        int exitCode = ExitCodes.Internal;
        string[] optionNames = Array.Empty<string>();
        bool json = args.Contains("--json", StringComparer.Ordinal);

        try
        {
            var jsonOption = new Option<bool>("--json") { Description = "Write the result as JSON on stdout.", Recursive = true };
            var root = new RootCommand("dvx: Dataverse plugin tooling");
            root.Options.Add(jsonOption);
            root.Subcommands.Add(DescribeCommand.Create(jsonOption, stdout, stderr, workingDirectory, outcome, describe));

            ParseResult parseResult = root.Parse(args);
            command = parseResult.CommandResult.Command.Name;
            optionNames = OptionNames(parseResult);

            if (parseResult.Errors.Count > 0)
            {
                var diagnostics = parseResult.Errors.Select(e => Output.Error(DiagnosticCodes.Usage, e.Message)).ToArray();
                outcome.DiagnosticCodes.Add(DiagnosticCodes.Usage);
                Output.WriteError(stdout, stderr, json, diagnostics);
                exitCode = ExitCodes.Usage;
            }
            else
            {
                exitCode = parseResult.Invoke(new InvocationConfiguration { Output = stdout, Error = stderr, EnableDefaultExceptionHandler = false });
            }
        }
        catch (Exception ex)
        {
            outcome.DiagnosticCodes.Add(DiagnosticCodes.Internal);
            Output.WriteError(stdout, stderr, json, new[] { Output.Error(DiagnosticCodes.Internal, "Internal error: " + ex.Message) });
            exitCode = ExitCodes.Internal;
        }
        finally
        {
            try
            {
                string root = outcome.RepoRoot ?? CommandLog.FindRepoRoot(workingDirectory) ?? Path.GetFullPath(workingDirectory);
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

    /// <summary>Names of options the user typed, taken from the parser's option tokens; values are never recorded.</summary>
    private static string[] OptionNames(ParseResult parseResult)
    {
        var names = new List<string>();
        for (SymbolResult? result = parseResult.CommandResult; result != null; result = result.Parent)
        {
            if (result is CommandResult commandResult)
            {
                names.AddRange(commandResult.Children.OfType<OptionResult>()
                    .Select(o => o.IdentifierToken?.Value)
                    .Where(v => !string.IsNullOrEmpty(v))!);
            }
        }

        return names.Distinct(StringComparer.Ordinal).ToArray();
    }
}
