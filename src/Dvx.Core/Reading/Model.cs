using System;
using System.Collections.Generic;

namespace Dvx.Core.Reading;

/// <summary>Registration facts read from a built plugin assembly (metadata only).</summary>
public sealed record AssemblyInfo(
    string Name,
    Version Version,
    string Culture,
    string PublicKeyToken,
    string SdkVersion,
    IReadOnlyList<PluginTypeInfo> Types,
    IReadOnlyList<EntityConstantsInfo> Entities);

public sealed record PluginTypeInfo(
    string FullName,
    string? SourceFile,
    IReadOnlyList<PluginStepInfo> Steps,
    CustomApiInfo? CustomApi,
    bool IsEntityPlugin);

/// <summary>Stage and Mode carry the raw SDK enum values (Stage 10/20/40, Mode 0/1).</summary>
public sealed record PluginStepInfo(
    string Message,
    string PrimaryEntity,
    int Stage,
    int Mode,
    IReadOnlyList<string>? FilteringAttributes,
    IReadOnlyList<string>? PreImageAttributes,
    IReadOnlyList<string>? PostImageAttributes,
    int Rank,
    string? Description,
    bool AsyncAutoDelete,
    string? UnsecureConfiguration);

public sealed record CustomApiInfo(
    string UniqueName,
    int BindingType,
    string? BoundEntityLogicalName,
    bool IsFunction,
    bool IsPrivate,
    int AllowedCustomProcessingStepType,
    string? ExecutePrivilegeName,
    string? DisplayName,
    IReadOnlyList<CustomApiParamInfo> Parameters);

public sealed record CustomApiParamInfo(string UniqueName, int Type, bool IsOptional, bool IsRequest);

public sealed record EntityConstantsInfo(
    string ClassFullName,
    string LogicalName,
    IReadOnlyList<(string Name, string Value)> Fields);
