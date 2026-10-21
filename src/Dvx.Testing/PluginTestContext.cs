using System;
using System.Collections.Generic;
using Dvx.Plugin.Sdk;
using FakeItEasy;
using Microsoft.Xrm.Sdk;

namespace Dvx.Testing
{
    /// <summary>
    /// Builds a fake plugin execution context around an <see cref="InMemoryOrganizationService"/>.
    /// <see cref="Outputs"/> and <see cref="Trace"/> accumulate across multiple <see cref="Execute"/> calls
    /// on the same instance; create a new context per scenario for isolated results.
    /// </summary>
    internal sealed class PluginTestContext
    {
        private static readonly Guid FixedUserId = new Guid("00000000-0000-0000-0000-0000000000a1");

        private readonly string _message;
        private readonly string _entity;
        private readonly Stage _stage;
        private readonly ParameterCollection _inputs = new ParameterCollection();
        private readonly EntityImageCollection _preImages = new EntityImageCollection();
        private readonly EntityImageCollection _postImages = new EntityImageCollection();
        private int _depth = 1;

        private PluginTestContext(string message, string entity, Stage stage)
        {
            _message = message;
            _entity = entity;
            _stage = stage;
            Service = new InMemoryOrganizationService();
            Trace = new List<string>();
            Outputs = new ParameterCollection();
        }

        public InMemoryOrganizationService Service { get; private set; }
        public IList<string> Trace { get; private set; }
        public ParameterCollection Outputs { get; private set; }

        public static PluginTestContext For(string message, string entity, Stage stage)
        {
            return new PluginTestContext(message, entity, stage);
        }

        public PluginTestContext WithTarget(Entity target)
        {
            _inputs["Target"] = target;
            return this;
        }

        public PluginTestContext WithPreImage(Entity image)
        {
            _preImages["PreImage"] = image;
            return this;
        }

        public PluginTestContext WithPostImage(Entity image)
        {
            _postImages["PostImage"] = image;
            return this;
        }

        public PluginTestContext WithInput(string name, object value)
        {
            _inputs[name] = value;
            return this;
        }

        public PluginTestContext WithDepth(int depth)
        {
            _depth = depth;
            return this;
        }

        /// <summary>Runs the plugin with a freshly built service provider.</summary>
        public void Execute(IPlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException("plugin");
            plugin.Execute(BuildProvider());
        }

        private IServiceProvider BuildProvider()
        {
            // The IPluginExecutionContext2..7 interfaces ship in the referenced Microsoft.Xrm.Sdk 9.0.2.60
            // (verified by reflection at dev time); the fake implements them all so plugins asking for a
            // newer interface get the same configured context.
            var context = A.Fake<IPluginExecutionContext>(o => o
                .Implements<IPluginExecutionContext2>()
                .Implements<IPluginExecutionContext3>()
                .Implements<IPluginExecutionContext4>()
                .Implements<IPluginExecutionContext5>()
                .Implements<IPluginExecutionContext6>()
                .Implements<IPluginExecutionContext7>());
            var tracing = A.Fake<ITracingService>();
            var factory = A.Fake<IOrganizationServiceFactory>();
            var provider = A.Fake<IServiceProvider>();

            var target = _inputs.Contains("Target") ? _inputs["Target"] as Entity : null;
            A.CallTo(() => context.MessageName).Returns(_message);
            A.CallTo(() => context.PrimaryEntityName).Returns(_entity);
            A.CallTo(() => context.Stage).Returns((int)_stage);
            A.CallTo(() => context.Depth).Returns(_depth);
            A.CallTo(() => context.InputParameters).Returns(_inputs);
            A.CallTo(() => context.OutputParameters).Returns(Outputs);
            A.CallTo(() => context.PreEntityImages).Returns(_preImages);
            A.CallTo(() => context.PostEntityImages).Returns(_postImages);
            A.CallTo(() => context.SharedVariables).Returns(new ParameterCollection());
            A.CallTo(() => context.ParentContext).Returns(null);
            A.CallTo(() => context.UserId).Returns(FixedUserId);
            A.CallTo(() => context.InitiatingUserId).Returns(FixedUserId);
            if (target != null) A.CallTo(() => context.PrimaryEntityId).Returns(target.Id);

            A.CallTo(() => tracing.Trace(A<string>._, A<object[]>._))
                .Invokes((string format, object[] args) =>
                    Trace.Add(args != null && args.Length > 0 ? string.Format(format, args) : format));
            A.CallTo(() => factory.CreateOrganizationService(A<Guid?>._)).Returns(Service);

            // Unknown service types resolve to null (like a real provider); only the configured ones are returned.
            A.CallTo(() => provider.GetService(A<Type>._)).Returns(null);
            foreach (var contextType in new[]
            {
                typeof(IExecutionContext), typeof(IPluginExecutionContext), typeof(IPluginExecutionContext2),
                typeof(IPluginExecutionContext3), typeof(IPluginExecutionContext4), typeof(IPluginExecutionContext5),
                typeof(IPluginExecutionContext6), typeof(IPluginExecutionContext7)
            })
            {
                var type = contextType;
                A.CallTo(() => provider.GetService(type)).Returns(context);
            }
            A.CallTo(() => provider.GetService(typeof(ITracingService))).Returns(tracing);
            A.CallTo(() => provider.GetService(typeof(IOrganizationServiceFactory))).Returns(factory);
            return provider;
        }
    }
}
