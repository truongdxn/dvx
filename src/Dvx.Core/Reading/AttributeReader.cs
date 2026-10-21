using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Dvx.Core.Reading;

/// <summary>
/// Reads registration attributes from a built plugin assembly with <see cref="MetadataLoadContext"/>.
/// The assembly is never loaded for execution.
/// </summary>
public sealed class AttributeReader
{
    private const string PluginStepAttributeName = "Dvx.Plugin.Sdk.PluginStepAttribute";
    private const string CustomApiAttributeName = "Dvx.Plugin.Sdk.CustomApiAttribute";
    private const string RequestParameterAttributeName = "Dvx.Plugin.Sdk.CustomApiRequestParameterAttribute";
    private const string ResponsePropertyAttributeName = "Dvx.Plugin.Sdk.CustomApiResponsePropertyAttribute";
    private const string EntityPluginBaseName = "Dvx.Plugin.Sdk.EntityPluginBase";
    private const string CustomApiBaseName = "Dvx.Plugin.Sdk.CustomApiBase`2";
    private const string SdkInfoName = "Dvx.Plugin.Sdk.DvxSdkInfo";
    private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public AssemblyInfo Read(string dllPath, IReadOnlyList<string> referencePaths)
    {
        ArgumentException.ThrowIfNullOrEmpty(dllPath);
        ArgumentNullException.ThrowIfNull(referencePaths);

        string dllFullPath = Path.GetFullPath(dllPath);
        string directory = Path.GetDirectoryName(dllFullPath)!;
        // Plugin folder first (it carries Microsoft.Xrm.Sdk.dll), then compile-time references.
        IEnumerable<string> candidates = Directory.GetFiles(directory, "*.dll").Concat(referencePaths);
        var resolver = new PathAssemblyResolver(candidates.Distinct(StringComparer.OrdinalIgnoreCase));

        try
        {
            using var context = new MetadataLoadContext(resolver, "mscorlib");
            Assembly assembly = context.LoadFromAssemblyPath(dllFullPath);
            EnsureReferencesResolve(context, assembly);
            return ReadAssembly(assembly);
        }
        catch (FileNotFoundException ex)
        {
            throw new DvxReadException("DVX180", "A referenced assembly could not be resolved: " + ex.Message);
        }
    }

    private static void EnsureReferencesResolve(MetadataLoadContext context, Assembly assembly)
    {
        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
        {
            try
            {
                context.LoadFromAssemblyName(reference);
            }
            catch (FileNotFoundException)
            {
                throw new DvxReadException("DVX180", (reference.Name ?? reference.FullName) + " could not be resolved");
            }
        }
    }

    private static AssemblyInfo ReadAssembly(Assembly assembly)
    {
        AssemblyName name = assembly.GetName();
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            string details = string.Join("; ", ex.LoaderExceptions.Where(e => e != null).Select(e => e!.Message).Distinct());
            throw new DvxReadException("DVX180", "Types of '" + name.Name + "' could not be loaded: " + details);
        }

        var pluginTypes = new List<PluginTypeInfo>();
        var entities = new List<EntityConstantsInfo>();
        foreach (Type type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            PluginTypeInfo? pluginType = ReadPluginType(type);
            if (pluginType != null) pluginTypes.Add(pluginType);

            EntityConstantsInfo? entity = ReadEntityConstants(type);
            if (entity != null) entities.Add(entity);
        }

        return new AssemblyInfo(
            name.Name ?? throw new DvxReadException("DVX180", "The assembly has no name."),
            name.Version ?? new Version(0, 0, 0, 0),
            string.IsNullOrEmpty(name.CultureName) ? "neutral" : name.CultureName,
            ToHex(name.GetPublicKeyToken()),
            ReadSdkVersion(types),
            pluginTypes,
            entities);
    }

    private static string ReadSdkVersion(Type[] types)
    {
        Type? info = types.FirstOrDefault(t => t.FullName == SdkInfoName);
        FieldInfo? field = info?.GetField("Version", AllMembers);
        if (field?.GetRawConstantValue() is string version) return version;
        throw new DvxReadException("DVX183", "DvxSdkInfo not found");
    }

    private static string ToHex(byte[]? token) =>
        token == null ? "" : string.Concat(token.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

    // ---- plugin types -------------------------------------------------------------------------

    private static PluginTypeInfo? ReadPluginType(Type type)
    {
        IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
        var steps = new List<PluginStepInfo>();
        CustomApiInfo? customApi = null;

        foreach (CustomAttributeData attribute in attributes)
        {
            switch (attribute.AttributeType.FullName)
            {
                case PluginStepAttributeName:
                    steps.Add(ReadStep(type, attribute));
                    break;
                case CustomApiAttributeName:
                    customApi = ReadCustomApi(type, attribute);
                    break;
            }
        }

        if (steps.Count == 0 && customApi == null) return null;

        // Declaration order of attributes is not guaranteed by metadata: sort by identity-relevant fields.
        PluginStepInfo[] ordered = steps
            .OrderBy(s => s.Message, StringComparer.Ordinal)
            .ThenBy(s => s.PrimaryEntity, StringComparer.Ordinal)
            .ThenBy(s => s.Stage)
            .ToArray();

        return new PluginTypeInfo(type.FullName!, null, ordered, customApi, InheritsFrom(type, EntityPluginBaseName));
    }

    private static PluginStepInfo ReadStep(Type type, CustomAttributeData attribute)
    {
        IList<CustomAttributeTypedArgument> args = attribute.ConstructorArguments;
        if (args.Count != 4) throw new DvxReadException("DVX182", Where(type, attribute) + " has an unexpected constructor.");
        var named = NamedArguments(attribute);

        return new PluginStepInfo(
            RequiredString(args[0], type, attribute, "message"),
            RequiredString(args[1], type, attribute, "primaryEntity"),
            Convert.ToInt32(args[2].Value, CultureInfo.InvariantCulture),
            Convert.ToInt32(args[3].Value, CultureInfo.InvariantCulture),
            NamedStringArray(named, "FilteringAttributes", type, attribute),
            NamedStringArray(named, "PreImageAttributes", type, attribute),
            NamedStringArray(named, "PostImageAttributes", type, attribute),
            NamedInt(named, "Rank", 1), // the constructor default is not part of the metadata
            NamedString(named, "Description"),
            NamedBool(named, "AsyncAutoDelete"),
            NamedString(named, "UnsecureConfiguration"));
    }

    private static CustomApiInfo ReadCustomApi(Type type, CustomAttributeData attribute)
    {
        IList<CustomAttributeTypedArgument> args = attribute.ConstructorArguments;
        if (args.Count != 1) throw new DvxReadException("DVX182", Where(type, attribute) + " has an unexpected constructor.");
        var named = NamedArguments(attribute);

        var parameters = new List<CustomApiParamInfo>();
        Type? apiBase = FindBase(type, CustomApiBaseName);
        if (apiBase != null)
        {
            Type[] generics = apiBase.GetGenericArguments();
            parameters.AddRange(ReadParameters(generics[0], RequestParameterAttributeName, isRequest: true));
            parameters.AddRange(ReadParameters(generics[1], ResponsePropertyAttributeName, isRequest: false));
        }

        return new CustomApiInfo(
            RequiredString(args[0], type, attribute, "uniqueName"),
            NamedInt(named, "BindingType", 0),
            NamedString(named, "BoundEntityLogicalName"),
            NamedBool(named, "IsFunction"),
            NamedBool(named, "IsPrivate"),
            NamedInt(named, "AllowedCustomProcessingStepType", 0),
            NamedString(named, "ExecutePrivilegeName"),
            NamedString(named, "DisplayName"),
            parameters);
    }

    private static IEnumerable<CustomApiParamInfo> ReadParameters(Type dataType, string attributeName, bool isRequest)
    {
        var result = new List<CustomApiParamInfo>();
        foreach (PropertyInfo property in dataType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            foreach (CustomAttributeData attribute in property.GetCustomAttributesData())
            {
                if (attribute.AttributeType.FullName != attributeName) continue;
                IList<CustomAttributeTypedArgument> args = attribute.ConstructorArguments;
                if (args.Count != 2)
                {
                    throw new DvxReadException("DVX182", dataType.FullName + "." + property.Name + " has an unexpected parameter attribute.");
                }

                string uniqueName = args[0].Value as string
                    ?? throw new DvxReadException("DVX182", dataType.FullName + "." + property.Name + " has no unique name.");
                result.Add(new CustomApiParamInfo(
                    uniqueName,
                    Convert.ToInt32(args[1].Value, CultureInfo.InvariantCulture),
                    NamedBool(NamedArguments(attribute), "IsOptional"),
                    isRequest));
            }
        }

        return result.OrderBy(p => p.UniqueName, StringComparer.Ordinal);
    }

    // ---- entity constants ---------------------------------------------------------------------

    private static EntityConstantsInfo? ReadEntityConstants(Type type)
    {
        FieldInfo? logicalName = type.GetField("EntityLogicalName", AllMembers);
        if (logicalName == null || !logicalName.IsLiteral || logicalName.GetRawConstantValue() is not string value) return null;

        Type? fieldsType = type.GetNestedType("Fields", BindingFlags.Public | BindingFlags.NonPublic);
        if (fieldsType == null) return null;

        var fields = new List<(string Name, string Value)>();
        foreach (FieldInfo field in fieldsType.GetFields(AllMembers))
        {
            if (field.IsLiteral && field.GetRawConstantValue() is string fieldValue)
            {
                fields.Add((field.Name, fieldValue));
            }
        }

        fields.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return new EntityConstantsInfo(type.FullName!, value, fields);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static bool InheritsFrom(Type type, string fullName) => FindBase(type, fullName) != null;

    private static Type? FindBase(Type type, string fullNameOrGenericDefinition)
    {
        for (Type? current = type.BaseType; current != null; current = current.BaseType)
        {
            string? name = current.IsGenericType ? current.GetGenericTypeDefinition().FullName : current.FullName;
            if (name == fullNameOrGenericDefinition) return current;
        }

        return null;
    }

    private static string Where(Type type, CustomAttributeData attribute) =>
        type.FullName + " [" + attribute.AttributeType.Name + "]";

    private static Dictionary<string, CustomAttributeTypedArgument> NamedArguments(CustomAttributeData attribute) =>
        attribute.NamedArguments.ToDictionary(a => a.MemberName, a => a.TypedValue, StringComparer.Ordinal);

    private static string RequiredString(CustomAttributeTypedArgument argument, Type type, CustomAttributeData attribute, string parameter) =>
        argument.Value as string
            ?? throw new DvxReadException("DVX182", Where(type, attribute) + " has a null '" + parameter + "'.");

    private static string? NamedString(Dictionary<string, CustomAttributeTypedArgument> named, string name) =>
        named.TryGetValue(name, out CustomAttributeTypedArgument value) ? value.Value as string : null;

    private static int NamedInt(Dictionary<string, CustomAttributeTypedArgument> named, string name, int defaultValue) =>
        named.TryGetValue(name, out CustomAttributeTypedArgument value)
            ? Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)
            : defaultValue;

    private static bool NamedBool(Dictionary<string, CustomAttributeTypedArgument> named, string name) =>
        named.TryGetValue(name, out CustomAttributeTypedArgument value) && value.Value is true;

    /// <summary>Absent named argument and explicit null both give null; an empty array stays empty.</summary>
    private static IReadOnlyList<string>? NamedStringArray(
        Dictionary<string, CustomAttributeTypedArgument> named, string name, Type type, CustomAttributeData attribute)
    {
        if (!named.TryGetValue(name, out CustomAttributeTypedArgument value) || value.Value == null) return null;
        if (value.Value is not IReadOnlyCollection<CustomAttributeTypedArgument> elements)
        {
            throw new DvxReadException("DVX182", Where(type, attribute) + " has an unreadable '" + name + "'.");
        }

        var result = new List<string>(elements.Count);
        foreach (CustomAttributeTypedArgument element in elements)
        {
            result.Add(element.Value as string
                ?? throw new DvxReadException("DVX182", Where(type, attribute) + " has a null element in '" + name + "'."));
        }

        return result;
    }
}
