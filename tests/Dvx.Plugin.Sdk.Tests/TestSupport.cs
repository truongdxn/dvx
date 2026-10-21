using System;
using System.Collections.Generic;
using FakeItEasy;
using Microsoft.Xrm.Sdk;

namespace Dvx_Plugin_Sdk_Tests
{
    internal sealed class FakeServices
    {
        public IServiceProvider Provider { get; private set; }
        public IPluginExecutionContext Context { get; private set; }
        public ITracingService Tracing { get; private set; }
        public IOrganizationServiceFactory Factory { get; private set; }
        public ParameterCollection Inputs { get; private set; }
        public ParameterCollection Outputs { get; private set; }
        public EntityImageCollection PreImages { get; private set; }
        public EntityImageCollection PostImages { get; private set; }
        public List<string> Traces { get; private set; }

        public static FakeServices Create(int depth = 1, Entity target = null)
        {
            var s = new FakeServices
            {
                Provider = A.Fake<IServiceProvider>(),
                Context = A.Fake<IPluginExecutionContext>(),
                Tracing = A.Fake<ITracingService>(),
                Factory = A.Fake<IOrganizationServiceFactory>(),
                Inputs = new ParameterCollection(),
                Outputs = new ParameterCollection(),
                PreImages = new EntityImageCollection(),
                PostImages = new EntityImageCollection(),
                Traces = new List<string>()
            };
            if (target != null) s.Inputs["Target"] = target;
            A.CallTo(() => s.Context.InputParameters).Returns(s.Inputs);
            A.CallTo(() => s.Context.OutputParameters).Returns(s.Outputs);
            A.CallTo(() => s.Context.PreEntityImages).Returns(s.PreImages);
            A.CallTo(() => s.Context.PostEntityImages).Returns(s.PostImages);
            A.CallTo(() => s.Context.Depth).Returns(depth);
            A.CallTo(() => s.Context.MessageName).Returns("Update");
            A.CallTo(() => s.Context.PrimaryEntityName).Returns("account");
            A.CallTo(() => s.Tracing.Trace(A<string>._, A<object[]>._))
                .Invokes((string f, object[] a) =>
                {
                    lock (s.Traces) s.Traces.Add(a != null && a.Length > 0 ? string.Format(f, a) : f);
                });
            A.CallTo(() => s.Provider.GetService(typeof(IPluginExecutionContext))).Returns(s.Context);
            A.CallTo(() => s.Provider.GetService(typeof(ITracingService))).Returns(s.Tracing);
            A.CallTo(() => s.Provider.GetService(typeof(IOrganizationServiceFactory))).Returns(s.Factory);
            return s;
        }
    }
}
