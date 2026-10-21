using System;

namespace Dvx.Plugin.Sdk
{
    /// <summary>Declares one plugin step registration for the decorated class.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal sealed class PluginStepAttribute : Attribute
    {
        public PluginStepAttribute(string message, string primaryEntity, Stage stage, Mode mode)
        {
            Message = message;
            PrimaryEntity = primaryEntity;
            Stage = stage;
            Mode = mode;
            Rank = 1;
        }

        public string Message { get; set; }
        public string PrimaryEntity { get; set; }
        public Stage Stage { get; set; }
        public Mode Mode { get; set; }
        public string[] FilteringAttributes { get; set; }
        public string[] PreImageAttributes { get; set; }
        public string[] PostImageAttributes { get; set; }
        public int Rank { get; set; }
        public string Description { get; set; }
        public bool AsyncAutoDelete { get; set; }
        public string UnsecureConfiguration { get; set; }
    }
}
