using System;

namespace Dvx.Plugin.Sdk
{
    /// <summary>Declares a Custom API for the decorated class.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class CustomApiAttribute : Attribute
    {
        public CustomApiAttribute(string uniqueName)
        {
            UniqueName = uniqueName;
        }

        public string UniqueName { get; set; }
        public CustomApiBindingType BindingType { get; set; }
        public string BoundEntityLogicalName { get; set; }
        public bool IsFunction { get; set; }
        public bool IsPrivate { get; set; }
        public CustomProcessingStepType AllowedCustomProcessingStepType { get; set; }
        public string ExecutePrivilegeName { get; set; }
        public string DisplayName { get; set; }
    }

    /// <summary>Declares a Custom API request parameter on a request property.</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    internal sealed class CustomApiRequestParameterAttribute : Attribute
    {
        public CustomApiRequestParameterAttribute(string uniqueName, CustomApiFieldType type)
        {
            UniqueName = uniqueName;
            Type = type;
        }

        public string UniqueName { get; set; }
        public CustomApiFieldType Type { get; set; }
        public bool IsOptional { get; set; }
    }

    /// <summary>Declares a Custom API response property on a response property.</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    internal sealed class CustomApiResponsePropertyAttribute : Attribute
    {
        public CustomApiResponsePropertyAttribute(string uniqueName, CustomApiFieldType type)
        {
            UniqueName = uniqueName;
            Type = type;
        }

        public string UniqueName { get; set; }
        public CustomApiFieldType Type { get; set; }
    }
}
