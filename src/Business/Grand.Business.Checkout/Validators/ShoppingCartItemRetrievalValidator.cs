using FluentValidation;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Domain.Catalog;
using Grand.Domain.Common;
using Grand.Domain.Orders;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of retrieving a shopping cart item, the system must make sure that
/// 1. The product must have equal attributes as the provided attributes.
/// 2. The bundle products retrieved using the product service from the shopping item product must have equal attributes as the shopping cart item when shopping cart item product type is bundle
/// 3. The shopping cart voucher attributes must be equal to the shopping cart item voucher attributes when shopping cart item product is a gift voucher
/// 4. The shopping cart item entered price must not be null
/// 5. The customer entered price must not be null
/// 6. The shopping cart item entered price must be equal to the customer entered price and rules 4,5 hold true
public record ShoppingCartItemRetrievalContext(ShoppingCartItemContext ShoppingCartItemContext, double? CustomerEnteredPrice, IReadOnlyList<CustomAttribute>? Attributes, IProductService ProductService);

public record ShoppingCartItemContext(ShoppingCartItem ShoppingCartItem, Product Product);

public class ShoppingCartItemRetrievalValidator : AbstractValidator<ShoppingCartItemRetrievalContext>
{
    public ShoppingCartItemRetrievalValidator()
    {
        RuleFor(Product).Must(HaveSameAttributesAsShoppingCartItem);

        RuleFor(WholeContext).MustAsync(HaveBundleProductsWithSameAttributesAsShoppingCartItem).When(ProductTypeIsBundle).OverridePropertyName("BundleProducts");

        RuleFor(WholeContext).Must(HaveEqualShoppingCartItemAndShoppingCartVoucherAttributes).When(ProductIsGiftVoucher).OverridePropertyName("GiftVoucherAttributes");

        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .Must(HaveNonNullShoppingCartItemEnteredPrice).When(CustomerEnteredPriceIsNotNull, ApplyConditionTo.CurrentValidator)
            .Must(HaveNonNullCustomerEnteredPrice).When(ShoppingCartItemEnteredPriceIsNotNull, ApplyConditionTo.CurrentValidator)
            .Must(HaveEqualCustomerAndShoppingCartItemEnteredPrices).When(BothPricesAreSet, ApplyConditionTo.CurrentValidator)
            .OverridePropertyName("EnteredPrice");
    }

    private static readonly Expression<Func<ShoppingCartItemRetrievalContext, ShoppingCartItemRetrievalContext>> WholeContext = x => x;

    private static readonly Expression<Func<ShoppingCartItemRetrievalContext, Product>> Product = x => x.ShoppingCartItemContext.Product;

    private static bool HaveSameAttributesAsShoppingCartItem(ShoppingCartItemRetrievalContext context, Product product) =>
        ProductExtensions.AreProductAttributesEqual(product, context.ShoppingCartItemContext.ShoppingCartItem.Attributes, context.Attributes?.ToList(), false);

    private static async Task<bool> HaveBundleProductsWithSameAttributesAsShoppingCartItem(ShoppingCartItemRetrievalContext context, CancellationToken cancellationToken)
    {
        var bundleProductsTasks = context.ShoppingCartItemContext.Product.BundleProducts.Select(async bundleProduct => await context.ProductService.GetProductById(bundleProduct.ProductId));
        var bundleProducts = await Task.WhenAll(bundleProductsTasks);
        return bundleProducts.Where(product => product is not null).All(product => ProductExtensions.AreProductAttributesEqual(product, context.ShoppingCartItemContext.ShoppingCartItem.Attributes, context.Attributes?.ToList(), false));
    }

    private static bool ProductTypeIsBundle(ShoppingCartItemRetrievalContext context) => context.ShoppingCartItemContext.Product.ProductTypeId == ProductType.BundledProduct;

    private static bool HaveEqualShoppingCartItemAndShoppingCartVoucherAttributes(ShoppingCartItemRetrievalContext context)
    {
        GiftVoucherExtensions.GetGiftVoucherAttribute(context.Attributes?.ToList(), out var giftVoucherRecipientName1, out _, out var giftVoucherSenderName1, out _, out _);
        GiftVoucherExtensions.GetGiftVoucherAttribute(context.ShoppingCartItemContext.ShoppingCartItem.Attributes, out var giftVoucherRecipientName2, out _, out var giftVoucherSenderName2, out _, out _);
        return string.Equals(giftVoucherRecipientName1, giftVoucherRecipientName2, StringComparison.InvariantCultureIgnoreCase) && string.Equals(giftVoucherSenderName1, giftVoucherSenderName2, StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool ProductIsGiftVoucher(ShoppingCartItemRetrievalContext context) => context.ShoppingCartItemContext.Product.IsGiftVoucher;

    private static bool HaveNonNullShoppingCartItemEnteredPrice(ShoppingCartItemRetrievalContext context) => context.ShoppingCartItemContext.ShoppingCartItem.EnteredPrice.HasValue;

    private static bool CustomerEnteredPriceIsNotNull(ShoppingCartItemRetrievalContext context) => context.CustomerEnteredPrice.HasValue;

    private static bool HaveNonNullCustomerEnteredPrice(ShoppingCartItemRetrievalContext context) => context.CustomerEnteredPrice.HasValue;

    private static bool ShoppingCartItemEnteredPriceIsNotNull(ShoppingCartItemRetrievalContext context) => context.ShoppingCartItemContext.ShoppingCartItem.EnteredPrice.HasValue;

    private static bool BothPricesAreSet(ShoppingCartItemRetrievalContext context) => CustomerEnteredPriceIsNotNull(context) && ShoppingCartItemEnteredPriceIsNotNull(context);

    private static bool HaveEqualCustomerAndShoppingCartItemEnteredPrices(ShoppingCartItemRetrievalContext context) =>
        Math.Round(context.ShoppingCartItemContext.ShoppingCartItem.EnteredPrice!.Value, 2) == Math.Round(context.CustomerEnteredPrice!.Value, 2);
}