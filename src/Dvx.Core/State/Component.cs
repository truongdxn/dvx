using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Dvx.Core.Reading;

namespace Dvx.Core.State;

/// <summary>Kinds of registration components. Enum order is the component sort order.</summary>
public enum ComponentKind
{
    Assembly,
    PluginType,
    Step,
    Image,
    CustomApi,
    RequestParameter,
    ResponseProperty,
}

/// <summary>One desired registration component. Field names are Dataverse logical names.</summary>
public sealed record Component(
    ComponentKind Kind,
    Guid Id,
    string Key,
    Guid? ParentId,
    ImmutableSortedDictionary<string, string?> Fields);

public sealed record DesiredState(Component Assembly, IReadOnlyList<Component> Components, AssemblyInfo Source);

/// <summary>Fields that Dataverse does not allow to change after creation (SPEC §7).</summary>
public static class ComponentRules
{
    private static readonly IReadOnlySet<string> None = ImmutableHashSet<string>.Empty;

    private static readonly IReadOnlySet<string> CustomApiFields = ImmutableHashSet.Create(
        "uniquename", "bindingtype", "boundentitylogicalname", "isfunction", "allowedcustomprocessingsteptype");

    private static readonly IReadOnlySet<string> RequestParameterFields = ImmutableHashSet.Create(
        "uniquename", "type", "isoptional");

    private static readonly IReadOnlySet<string> ResponsePropertyFields = ImmutableHashSet.Create(
        "uniquename", "type");

    public static IReadOnlySet<string> ImmutableFields(ComponentKind kind) => kind switch
    {
        ComponentKind.CustomApi => CustomApiFields,
        ComponentKind.RequestParameter => RequestParameterFields,
        ComponentKind.ResponseProperty => ResponsePropertyFields,
        _ => None,
    };
}

/// <summary>Two components of one assembly computed the same ID: an internal error (SPEC §7).</summary>
public sealed class DuplicateComponentIdException : Exception
{
    public DuplicateComponentIdException(string message)
        : base(message)
    {
    }
}
