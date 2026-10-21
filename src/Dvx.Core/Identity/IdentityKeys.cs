using System;
using System.Globalization;

namespace Dvx.Core.Identity;

/// <summary>
/// Identity key formats and normalization, frozen as SPEC §7 (D33). Message, entity and unique names are
/// lowercased invariant; assembly and type names are exact and ordinal.
/// </summary>
public static class IdentityKeys
{
    public static string Assembly(string asm)
    {
        Require(asm, nameof(asm));
        return "asm:" + asm;
    }

    public static string PluginType(string asm, string type)
    {
        Require(asm, nameof(asm));
        Require(type, nameof(type));
        return "type:" + asm + "/" + type;
    }

    public static string Step(string asm, string type, string message, string entity, int stage)
    {
        Require(asm, nameof(asm));
        Require(type, nameof(type));
        Require(message, nameof(message));
        // "none" is the literal Dataverse uses for global messages.
        if (string.IsNullOrWhiteSpace(entity) || string.Equals(entity, "none", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "global messages without a primary entity are not supported yet; identity key undefined in SPEC §7",
                nameof(entity));
        }

        return "step:" + asm + "/" + type + "|" + message.ToLowerInvariant() + "|" + entity.ToLowerInvariant()
            + "|" + stage.ToString(CultureInfo.InvariantCulture);
    }

    public static string Image(string stepKey, bool pre)
    {
        Require(stepKey, nameof(stepKey));
        return "img:" + stepKey + (pre ? "|pre" : "|post");
    }

    public static string CustomApi(string uniqueName)
    {
        Require(uniqueName, nameof(uniqueName));
        return "api:" + uniqueName.ToLowerInvariant();
    }

    public static string RequestParameter(string api, string param)
    {
        Require(api, nameof(api));
        Require(param, nameof(param));
        return "apireq:" + api.ToLowerInvariant() + "|" + param.ToLowerInvariant();
    }

    public static string ResponseProperty(string api, string property)
    {
        Require(api, nameof(api));
        Require(property, nameof(property));
        return "apiresp:" + api.ToLowerInvariant() + "|" + property.ToLowerInvariant();
    }

    private static void Require(string? value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Value must not be null or empty.", paramName);
        }
    }
}
