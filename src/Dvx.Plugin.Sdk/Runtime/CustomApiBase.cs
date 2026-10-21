using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xrm.Sdk;

namespace Dvx.Plugin.Sdk
{
    /// <summary>Custom API plugin: maps InputParameters to a request and a response to OutputParameters.</summary>
    internal abstract class CustomApiBase<TRequest, TResponse> : PluginBase
        where TRequest : new()
    {
        private sealed class ParameterMap
        {
            public ParameterMap(PropertyInfo[] requestProperties, string[] requestNames,
                PropertyInfo[] responseProperties, string[] responseNames)
            {
                RequestProperties = requestProperties;
                RequestNames = requestNames;
                ResponseProperties = responseProperties;
                ResponseNames = responseNames;
            }

            public PropertyInfo[] RequestProperties { get; private set; }
            public string[] RequestNames { get; private set; }
            public PropertyInfo[] ResponseProperties { get; private set; }
            public string[] ResponseNames { get; private set; }
        }

        // The only static state allowed (SPEC 6.4): an immutable reflection cache in a thread-safe collection.
        private static readonly ConcurrentDictionary<Type, ParameterMap> Maps =
            new ConcurrentDictionary<Type, ParameterMap>();

        protected abstract TResponse Handle(TRequest request, PluginRuntime runtime);

        protected override void ExecuteCore(PluginRuntime runtime)
        {
            var map = Maps.GetOrAdd(GetType(), _ => BuildMap());
            var inputs = runtime.PluginContext.InputParameters;

            var request = new TRequest();
            for (int i = 0; i < map.RequestProperties.Length; i++)
            {
                var name = map.RequestNames[i];
                var property = map.RequestProperties[i];
                if (inputs == null || !inputs.Contains(name)) continue;

                var value = inputs[name];
                if (value == null) continue;
                if (!property.PropertyType.IsInstanceOfType(value))
                {
                    throw new InvalidPluginExecutionException("Custom API parameter '" + name + "' has type "
                        + value.GetType().Name + " but " + property.PropertyType.Name + " is expected.");
                }
                property.SetValue(request, value, null);
            }

            var response = Handle(request, runtime);
            if (response == null)
            {
                throw new InvalidPluginExecutionException("Custom API handler returned no response.");
            }

            var outputs = runtime.PluginContext.OutputParameters;
            for (int i = 0; i < map.ResponseProperties.Length; i++)
            {
                outputs[map.ResponseNames[i]] = map.ResponseProperties[i].GetValue(response, null);
            }
        }

        private static ParameterMap BuildMap()
        {
            var reqProps = new List<PropertyInfo>();
            var reqNames = new List<string>();
            foreach (var p in typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = (CustomApiRequestParameterAttribute)Attribute.GetCustomAttribute(
                    p, typeof(CustomApiRequestParameterAttribute));
                if (attr == null || !p.CanWrite) continue;
                reqProps.Add(p);
                reqNames.Add(attr.UniqueName);
            }

            var respProps = new List<PropertyInfo>();
            var respNames = new List<string>();
            foreach (var p in typeof(TResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = (CustomApiResponsePropertyAttribute)Attribute.GetCustomAttribute(
                    p, typeof(CustomApiResponsePropertyAttribute));
                if (attr == null || !p.CanRead) continue;
                respProps.Add(p);
                respNames.Add(attr.UniqueName);
            }

            return new ParameterMap(reqProps.ToArray(), reqNames.ToArray(), respProps.ToArray(), respNames.ToArray());
        }
    }
}
