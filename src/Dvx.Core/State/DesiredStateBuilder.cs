using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Dvx.Core.Identity;
using Dvx.Core.Reading;

namespace Dvx.Core.State;

/// <summary>Turns a read assembly into components with computed identity keys and IDs.</summary>
public sealed class DesiredStateBuilder
{
    private const string AllColumnsMarker = "*";

    public DesiredState Build(AssemblyInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        try
        {
            return BuildCore(info);
        }
        catch (ArgumentException ex)
        {
            // An empty assembly, type or API name: no valid identity key exists (SPEC §7).
            throw new DvxReadException(DiagnosticCodes.IdentityInvalid, "A registration has no valid identity: " + ex.Message);
        }
    }

    private static DesiredState BuildCore(AssemblyInfo info)
    {
        var components = new List<Component>();
        Component assembly = Make(ComponentKind.Assembly, IdentityKeys.Assembly(info.Name), null, Fields(
            ("name", info.Name),
            ("version", info.Version.ToString()),
            ("culture", info.Culture),
            ("publickeytoken", info.PublicKeyToken)));
        components.Add(assembly);

        foreach (PluginTypeInfo type in info.Types)
        {
            Component typeComponent = Make(ComponentKind.PluginType, IdentityKeys.PluginType(info.Name, type.FullName), assembly.Id, Fields(
                ("typename", type.FullName),
                ("name", type.FullName)));
            components.Add(typeComponent);

            foreach (PluginStepInfo step in type.Steps)
            {
                AddStep(components, info.Name, type, typeComponent, step);
            }

            if (type.CustomApi is { } api)
            {
                AddCustomApi(components, typeComponent, api);
            }
        }

        Component[] ordered = components
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .ToArray();

        var seen = new Dictionary<Guid, string>();
        foreach (Component component in ordered)
        {
            if (!seen.TryAdd(component.Id, component.Key))
            {
                throw new DuplicateComponentIdException(
                    "Components '" + seen[component.Id] + "' and '" + component.Key + "' compute the same ID " + component.Id + ".");
            }
        }

        return new DesiredState(assembly, ordered, info);
    }

    private static void AddStep(List<Component> components, string assemblyName, PluginTypeInfo type, Component typeComponent, PluginStepInfo step)
    {
        string stepKey;
        try
        {
            stepKey = IdentityKeys.Step(assemblyName, type.FullName, step.Message, step.PrimaryEntity, step.Stage);
        }
        catch (ArgumentException ex)
        {
            // Typically a step without a primary entity: its identity key is not defined (SPEC §7).
            throw new DvxReadException(DiagnosticCodes.IdentityInvalid, type.FullName + ": step '" + step.Message + "' has no valid identity: " + ex.Message);
        }

        Component stepComponent = Make(ComponentKind.Step, stepKey, typeComponent.Id, Fields(
            ("sdkmessage", step.Message.ToLowerInvariant()),
            ("primaryobjecttypecode", step.PrimaryEntity.ToLowerInvariant()),
            ("name", type.FullName + ": " + step.Message + " of " + step.PrimaryEntity),
            ("stage", Number(step.Stage)),
            ("mode", Number(step.Mode)),
            ("rank", Number(step.Rank)),
            ("description", step.Description),
            ("configuration", step.UnsecureConfiguration),
            ("asyncautodelete", Bool(step.AsyncAutoDelete)),
            ("filteringattributes", ColumnList(step.FilteringAttributes))));
        components.Add(stepComponent);

        if (step.PreImageAttributes != null)
        {
            components.Add(Image(stepKey, stepComponent, pre: true, step.PreImageAttributes));
        }

        if (step.PostImageAttributes != null)
        {
            components.Add(Image(stepKey, stepComponent, pre: false, step.PostImageAttributes));
        }
    }

    private static Component Image(string stepKey, Component step, bool pre, IReadOnlyList<string> attributes)
    {
        string alias = pre ? "PreImage" : "PostImage";
        return Make(ComponentKind.Image, IdentityKeys.Image(stepKey, pre), step.Id, Fields(
            ("imagetype", pre ? "0" : "1"),
            ("entityalias", alias),
            ("attributes", ColumnList(attributes)),
            ("name", alias)));
    }

    private static void AddCustomApi(List<Component> components, Component handlerType, CustomApiInfo api)
    {
        Component apiComponent = Make(ComponentKind.CustomApi, IdentityKeys.CustomApi(api.UniqueName), handlerType.Id, Fields(
            ("uniquename", api.UniqueName),
            ("name", api.UniqueName),
            ("bindingtype", Number(api.BindingType)),
            ("boundentitylogicalname", api.BoundEntityLogicalName),
            ("isfunction", Bool(api.IsFunction)),
            ("isprivate", Bool(api.IsPrivate)),
            ("allowedcustomprocessingsteptype", Number(api.AllowedCustomProcessingStepType)),
            ("executeprivilegename", api.ExecutePrivilegeName),
            ("displayname", api.DisplayName)));
        components.Add(apiComponent);

        foreach (CustomApiParamInfo parameter in api.Parameters)
        {
            if (parameter.IsRequest)
            {
                components.Add(Make(ComponentKind.RequestParameter, IdentityKeys.RequestParameter(api.UniqueName, parameter.UniqueName), apiComponent.Id, Fields(
                    ("uniquename", parameter.UniqueName),
                    ("type", Number(parameter.Type)),
                    ("isoptional", Bool(parameter.IsOptional)),
                    ("name", parameter.UniqueName))));
            }
            else
            {
                components.Add(Make(ComponentKind.ResponseProperty, IdentityKeys.ResponseProperty(api.UniqueName, parameter.UniqueName), apiComponent.Id, Fields(
                    ("uniquename", parameter.UniqueName),
                    ("type", Number(parameter.Type)),
                    ("name", parameter.UniqueName))));
            }
        }
    }

    private static Component Make(ComponentKind kind, string key, Guid? parentId, ImmutableSortedDictionary<string, string?> fields) =>
        new(kind, Uuid5.Create(DvxNamespace.Value, key), key, parentId, fields);

    private static ImmutableSortedDictionary<string, string?> Fields(params (string Name, string? Value)[] fields)
    {
        ImmutableSortedDictionary<string, string?>.Builder builder = ImmutableSortedDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
        foreach ((string name, string? value) in fields)
        {
            builder[name] = value;
        }

        return builder.ToImmutable();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";

    /// <summary>Sorted, comma-joined; the All marker and an empty list become an empty string; absent stays null.</summary>
    private static string? ColumnList(IReadOnlyList<string>? columns)
    {
        if (columns == null) return null;
        if (columns.Contains(AllColumnsMarker, StringComparer.Ordinal)) return "";
        return string.Join(",", columns.OrderBy(c => c, StringComparer.Ordinal));
    }
}
