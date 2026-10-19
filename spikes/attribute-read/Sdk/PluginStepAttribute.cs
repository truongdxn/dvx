using System;

namespace Dvx.Plugin.Sdk
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class PluginStepAttribute : Attribute
    {
        public PluginStepAttribute(string message, string primaryEntity, Stage stage, Mode mode)
        {
            Message = message;
            PrimaryEntity = primaryEntity;
            Stage = stage;
            Mode = mode;
        }

        public string Message { get; }
        public string PrimaryEntity { get; }
        public Stage Stage { get; }
        public Mode Mode { get; }

        public string[] FilteringAttributes { get; set; }
        public string[] PreImageAttributes { get; set; }
        public string[] PostImageAttributes { get; set; }
        public int Rank { get; set; } = 1;
        public string Description { get; set; }
    }
}
