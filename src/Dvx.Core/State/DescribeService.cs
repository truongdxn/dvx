using System;
using System.Collections.Generic;
using System.Linq;
using Dvx.Contracts;
using Dvx.Core.Reading;

namespace Dvx.Core.State;

/// <summary>Builds the plugin project, reads its assembly and returns the desired registration.</summary>
public static class DescribeService
{
    public static DescribeResult Describe(string projectPath)
    {
        ProjectOutput output = ProjectOutputResolver.Resolve(projectPath, build: true);
        AssemblyInfo info = new AttributeReader().Read(output.TargetPath, output.ReferencePaths);
        return ToResult(new DesiredStateBuilder().Build(info));
    }

    public static DescribeResult ToResult(DesiredState state) => new()
    {
        AssemblyName = state.Assembly.Fields["name"] ?? "",
        Components = state.Components.Select(ToDto).ToArray(),
    };

    private static ComponentDto ToDto(Component component) => new()
    {
        Kind = component.Kind.ToString(),
        Id = component.Id.ToString(),
        Key = component.Key,
        ParentId = component.ParentId?.ToString(),
        Fields = new SortedDictionary<string, string?>(component.Fields, StringComparer.Ordinal),
    };
}
