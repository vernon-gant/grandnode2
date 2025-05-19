using FluentValidation;
using Grand.Domain.Catalog;
using System.Linq.Expressions;

namespace Grand.Business.Catalog.Services.Validators;

/// <summary>
/// In the context of publishing the updated product on cart event, the system must ensure that:
/// 1. The old product additional shipping charge must be equal to new product additional shipping charge.
/// 2. The old product free-shipping flag must be equal to new product free-shipping flag when rule 1 holds true.
/// 3. The old product gift voucher flag must be equal to new product gift voucher flag when rule 2 holds true.
/// 4. The old product shipping enabled flag must be equal to new product shipping enabled flag when rule 3 holds true.
/// 5. The old product tax exempt flag must be equal to new product tax exempt flag when rule 4 holds true.
/// 6. The old product recurring flag must be equal to new product recurring flag when rule 5 holds true.
/// </summary>
public record ProductCartUpdatePublishingContext(Product OldProduct, Product NewProduct);

public class ProductCartUpdatePublishingValidator : AbstractValidator<ProductCartUpdatePublishingContext>
{
    public ProductCartUpdatePublishingValidator()
    {
        RuleFor(OldProduct).Cascade(CascadeMode.Stop)
            .Must(HaveSameAdditionalShippingChargeWithNewProduct).Must(HaveSameFreeShippingWithNewProduct).Must(HaveSameGiftVoucherWithNewProduct)
            .Must(HaveSameShipEnabledWithNewProduct).Must(HaveSameTaxExemptWithNewProduct).Must(HaveSameRecurringWithNewProduct);
    }

    private static readonly Expression<Func<ProductCartUpdatePublishingContext, Product>> OldProduct = context => context.OldProduct;

    private static bool HaveSameAdditionalShippingChargeWithNewProduct(ProductCartUpdatePublishingContext context, Product oldProduct) => oldProduct.AdditionalShippingCharge.Equals(context.NewProduct.AdditionalShippingCharge);

    private static bool HaveSameFreeShippingWithNewProduct(ProductCartUpdatePublishingContext context, Product oldProduct) => oldProduct.IsFreeShipping.Equals(context.NewProduct.IsFreeShipping);

    private static bool HaveSameGiftVoucherWithNewProduct(ProductCartUpdatePublishingContext context, Product oldProduct) => oldProduct.IsGiftVoucher.Equals(context.NewProduct.IsGiftVoucher);

    private static bool HaveSameShipEnabledWithNewProduct(ProductCartUpdatePublishingContext context, Product oldProduct) => oldProduct.IsShipEnabled.Equals(context.NewProduct.IsShipEnabled);

    private static bool HaveSameTaxExemptWithNewProduct(ProductCartUpdatePublishingContext context, Product oldProduct) => oldProduct.IsTaxExempt.Equals(context.NewProduct.IsTaxExempt);

    private static bool HaveSameRecurringWithNewProduct(ProductCartUpdatePublishingContext context, Product oldProduct) => oldProduct.IsRecurring.Equals(context.NewProduct.IsRecurring);
}