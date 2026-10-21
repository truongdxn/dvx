using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace Dvx.Plugin.Sdk
{
    /// <summary>Per-execution view of the plugin context. Created per Execute call; never stored on the plugin.</summary>
    internal sealed class PluginRuntime
    {
        private const string PreImageAlias = "PreImage";
        private const string PostImageAlias = "PostImage";

        private readonly IOrganizationServiceFactory _factory;
        private IOrganizationService _userService;
        private IOrganizationService _systemService;

        public PluginRuntime(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException("serviceProvider");

            PluginContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            Tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            _factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            Shared = new Dictionary<string, object>();

            Target = PluginContext.InputParameters != null && PluginContext.InputParameters.Contains("Target")
                ? PluginContext.InputParameters["Target"] as Entity
                : null;
            PreImage = Image(PluginContext.PreEntityImages, PreImageAlias);
            PostImage = Image(PluginContext.PostEntityImages, PostImageAlias);
        }

        public IPluginExecutionContext PluginContext { get; private set; }
        public ITracingService Tracing { get; private set; }
        public Entity Target { get; private set; }
        public Entity PreImage { get; private set; }
        public Entity PostImage { get; private set; }
        public IDictionary<string, object> Shared { get; private set; }

        /// <summary>Service running as the initiating user. Created on first access.</summary>
        public IOrganizationService UserService
        {
            get
            {
                if (_userService == null) _userService = _factory.CreateOrganizationService(PluginContext.UserId);
                return _userService;
            }
        }

        /// <summary>Service running as the system user. Created on first access.</summary>
        public IOrganizationService SystemService
        {
            get
            {
                if (_systemService == null) _systemService = _factory.CreateOrganizationService(null);
                return _systemService;
            }
        }

        /// <summary>Writes one trace line. The text is never used as a format string.</summary>
        public void Trace(string message)
        {
            if (Tracing != null) Tracing.Trace("{0}", message);
        }

        private static Entity Image(EntityImageCollection images, string alias)
        {
            return images != null && images.Contains(alias) ? images[alias] : null;
        }
    }
}
