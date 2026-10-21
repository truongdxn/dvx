using System;
using System.Linq;
using System.Reflection;
using Dvx.Plugin.Sdk;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dvx_Plugin_Sdk_Tests
{
    [TestClass]
    public class AttributeTests
    {
        [PluginStep("Update", "account", Stage.PreOperation, Mode.Synchronous,
            FilteringAttributes = new[] { FilteringAttributes.All },
            PreImageAttributes = new[] { ImageAttributes.All },
            Description = "d", AsyncAutoDelete = true, UnsecureConfiguration = "cfg")]
        private sealed class SampleStep { }

        [CustomApi("contoso_Calc",
            BindingType = CustomApiBindingType.Entity,
            BoundEntityLogicalName = "account",
            IsFunction = true,
            IsPrivate = true,
            AllowedCustomProcessingStepType = CustomProcessingStepType.SyncAndAsync,
            ExecutePrivilegeName = "prvCalc",
            DisplayName = "Calc")]
        private sealed class SampleApi { }

        private sealed class SampleRequest
        {
            [CustomApiRequestParameter("contoso_Amount", CustomApiFieldType.Money)]
            public decimal Amount { get; set; }

            [CustomApiRequestParameter("contoso_Customer", CustomApiFieldType.EntityReference, IsOptional = true)]
            public object Customer { get; set; }
        }

        private sealed class SampleResponse
        {
            [CustomApiResponseProperty("contoso_Discount", CustomApiFieldType.Decimal)]
            public decimal Discount { get; set; }
        }

        [TestMethod]
        public void PluginStep_DefaultRank_IsOne() =>
            Assert.AreEqual(1, new PluginStepAttribute("Update", "account", Stage.PreOperation, Mode.Synchronous).Rank);

        [TestMethod]
        public void Markers_AreStar()
        {
            Assert.AreEqual("*", typeof(FilteringAttributes).GetField("All").GetValue(null));
            Assert.AreEqual("*", typeof(ImageAttributes).GetField("All").GetValue(null));
        }

        [TestMethod]
        public void Stage_Values_MatchDataverse()
        {
            Assert.AreEqual(10, Convert.ToInt32((object)Stage.PreValidation));
            Assert.AreEqual(20, Convert.ToInt32((object)Stage.PreOperation));
            Assert.AreEqual(40, Convert.ToInt32((object)Stage.PostOperation));
            Assert.AreEqual(0, Convert.ToInt32((object)Mode.Synchronous));
            Assert.AreEqual(1, Convert.ToInt32((object)Mode.Asynchronous));
        }

        [TestMethod]
        public void CustomApiEnums_Values_MatchDataverse()
        {
            Assert.AreEqual(2, Convert.ToInt32((object)CustomApiBindingType.EntityCollection));
            Assert.AreEqual(2, Convert.ToInt32((object)CustomProcessingStepType.SyncAndAsync));
            Assert.AreEqual(8, Convert.ToInt32((object)CustomApiFieldType.Money));
            Assert.AreEqual(12, Convert.ToInt32((object)CustomApiFieldType.Guid));
        }

        [TestMethod]
        public void PluginStep_RealUsage_ReadsMarkerValues()
        {
            var a = typeof(SampleStep).GetCustomAttributes(typeof(PluginStepAttribute), false)
                .Cast<PluginStepAttribute>().Single();
            CollectionAssert.AreEqual(new[] { "*" }, a.FilteringAttributes);
            CollectionAssert.AreEqual(new[] { "*" }, a.PreImageAttributes);
            Assert.IsNull(a.PostImageAttributes);
            Assert.AreEqual("Update", a.Message);
            Assert.AreEqual("account", a.PrimaryEntity);
            Assert.AreEqual(Stage.PreOperation, a.Stage);
            Assert.AreEqual(Mode.Synchronous, a.Mode);
            Assert.AreEqual("d", a.Description);
            Assert.IsTrue(a.AsyncAutoDelete);
            Assert.AreEqual("cfg", a.UnsecureConfiguration);
        }

        [TestMethod]
        public void CustomApi_RealUsage_ReadsValues()
        {
            var a = typeof(SampleApi).GetCustomAttributes(typeof(CustomApiAttribute), false)
                .Cast<CustomApiAttribute>().Single();
            Assert.AreEqual("contoso_Calc", a.UniqueName);
            Assert.AreEqual(CustomApiBindingType.Entity, a.BindingType);
            Assert.AreEqual("account", a.BoundEntityLogicalName);
            Assert.IsTrue(a.IsFunction);
            Assert.IsTrue(a.IsPrivate);
            Assert.AreEqual(CustomProcessingStepType.SyncAndAsync, a.AllowedCustomProcessingStepType);
            Assert.AreEqual("prvCalc", a.ExecutePrivilegeName);
            Assert.AreEqual("Calc", a.DisplayName);

            var amount = typeof(SampleRequest).GetProperty("Amount")
                .GetCustomAttribute<CustomApiRequestParameterAttribute>();
            Assert.AreEqual(CustomApiFieldType.Money, amount.Type);
            Assert.IsFalse(amount.IsOptional);
            var cust = typeof(SampleRequest).GetProperty("Customer")
                .GetCustomAttribute<CustomApiRequestParameterAttribute>();
            Assert.IsTrue(cust.IsOptional);
            var resp = typeof(SampleResponse).GetProperty("Discount")
                .GetCustomAttribute<CustomApiResponsePropertyAttribute>();
            Assert.AreEqual("contoso_Discount", resp.UniqueName);
        }

        [TestMethod]
        public void AttributeUsage_IsAsDecided()
        {
            var step = typeof(PluginStepAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.IsTrue(step.AllowMultiple);
            Assert.IsFalse(step.Inherited);
            Assert.AreEqual(AttributeTargets.Class, step.ValidOn);
            var api = typeof(CustomApiAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.IsFalse(api.AllowMultiple);
            Assert.AreEqual(AttributeTargets.Class, api.ValidOn);
            Assert.AreEqual(AttributeTargets.Property,
                typeof(CustomApiRequestParameterAttribute).GetCustomAttribute<AttributeUsageAttribute>().ValidOn);
            Assert.AreEqual("0.1.0", typeof(DvxSdkInfo).GetField("Version").GetValue(null));
        }

        [TestMethod]
        public void IdentityProperties_HaveNoPublicSetter()
        {
            var identity = new[]
            {
                typeof(PluginStepAttribute).GetProperty("Message"),
                typeof(PluginStepAttribute).GetProperty("PrimaryEntity"),
                typeof(PluginStepAttribute).GetProperty("Stage"),
                typeof(PluginStepAttribute).GetProperty("Mode"),
                typeof(CustomApiAttribute).GetProperty("UniqueName"),
                typeof(CustomApiRequestParameterAttribute).GetProperty("UniqueName"),
                typeof(CustomApiRequestParameterAttribute).GetProperty("Type"),
                typeof(CustomApiResponsePropertyAttribute).GetProperty("UniqueName"),
                typeof(CustomApiResponsePropertyAttribute).GetProperty("Type")
            };
            foreach (var p in identity)
            {
                Assert.IsNotNull(p);
                var setter = p.GetSetMethod(false);
                Assert.IsNull(setter, p.DeclaringType.Name + "." + p.Name + " must not have a public setter");
            }
        }
    }
}
