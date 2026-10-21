using Dvx.Plugin.Sdk;
using Microsoft.Xrm.Sdk;

namespace Contoso.Plugins
{
    [CustomApi("contoso_CalculateDiscount",
        BindingType = CustomApiBindingType.Global,
        IsFunction = false,
        IsPrivate = false,
        AllowedCustomProcessingStepType = CustomProcessingStepType.None,
        ExecutePrivilegeName = "prvContosoCalculateDiscount",
        DisplayName = "Calculate Discount")]
    internal sealed class CalculateDiscountApi
        : CustomApiBase<CalculateDiscountRequest, CalculateDiscountResponse>
    {
        protected override CalculateDiscountResponse Handle(CalculateDiscountRequest request, PluginRuntime runtime)
        {
            return new CalculateDiscountResponse { Discount = 0m };
        }
    }

    internal sealed class CalculateDiscountRequest
    {
        [CustomApiRequestParameter("contoso_Amount", CustomApiFieldType.Money)]
        public Money Amount { get; set; }

        [CustomApiRequestParameter("contoso_CustomerId", CustomApiFieldType.EntityReference, IsOptional = true)]
        public EntityReference Customer { get; set; }
    }

    internal sealed class CalculateDiscountResponse
    {
        [CustomApiResponseProperty("contoso_Discount", CustomApiFieldType.Decimal)]
        public decimal Discount { get; set; }
    }
}
