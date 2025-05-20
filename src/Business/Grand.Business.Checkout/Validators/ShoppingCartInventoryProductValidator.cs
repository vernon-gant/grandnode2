using FluentValidation;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using Grand.Domain.Customers;
using Grand.Domain.Orders;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating inventory for a shopping cart item, the system must ensure that:
/// 1. Requested quantity must be at least the product’s minimum order quantity.
/// 2. Requested quantity must not exceed the product’s maximum order quantity.
/// 3. Requested quantity must be one of the allowed quantities when the product defines allowed quantities.
/// 4. Warehouse id must not be empty when the setting "Allow to select warehouse" is enabled.
/// 5. Product quantity of the item with the same product id, store id and shopping cart type id in the shopping cart
///     must not exceed the available stock retrieved from the stock quantity service
///     when the product is managed by stock and does not allow backorders.
/// 6. Product of a bundle product which allows back orders and inventory method is managed by stock in product bundle list
///     must have the total needed bundle quantity which does not exceed its available stock retrieved from the stock service
///     when main product is managed by bundle products
/// 7. Product of a bundle product which allows back orders and inventory method is managed by stock in product bundle list
///     product attribute combination must be not null
///     when main product is managed by bundle products
/// 8. Product of a bundle product which allows back orders, inventory method is managed by bundle products and rule 7 holds true in product bundle list
///     must have the bundle quantity which does not exceed product available stock retrieved from the stock service with different error messages
///     when product attribute combination does not allow backorders and main product is managed by bundle products
/// 9. Product attribute combination must not be null when the inventory method is managed by attributes.
/// 10. Product attribute combination
///     must have the total needed quantity which does not exceed its available stock retrieved from the stock service with different error messages for out of stock and quantity exceeds stock
///     when inventory method is managed by attributes, combination does not allow out of stock orders and rule 9 holds true.
public record ShoppingCartInventoryProductContext(Customer Customer, Product Product, IReadOnlyList<BundleProductContext> BundleProducts, ShoppingCartItem ShoppingCartItem, IStockQuantityService StockQuantityService, bool AllowSelectWarehouse);

public record BundleProductContext(BundleProduct BundleProduct, Product Product);

public class ShoppingCartInventoryProductValidator : AbstractValidator<ShoppingCartInventoryProductContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartInventoryProductValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleFor(ItemQuantity)
            .GreaterThanOrEqualTo(ProductMinimumQuantity).WithMessage(MinimumQuantityMessage)
            .LessThanOrEqualTo(ProductMaximumQuantity).WithMessage(MaximumQuantityMessage)
            .Must(BeInTheListOfProductAllowedQuantities).When(AllowedProductQuantitiesIsNoEmpty).WithMessage(AllowedQuantitiesMessage);

        RuleFor(WarehouseId).NotEmpty().When(AllowSelectWarehouse).WithMessage(WarehouseRequiredMessage);

        When(ItemQuantityIsInValidRange, () =>
        {
            RuleFor(ProductQuantity).Must(BeLessThanOrEqualToTotalStock).WithMessage(StockExceededMessage).When(MainProductIsManagedByStockAndProductModeIsNoBackOrders);

            RuleForEach(BundleProducts).Where(UnderlyingProductAllowsBackordersAndIsManagedByStock)
                .Must(HaveBundleStockWhichDoesNotExceedUnderlyingProductStock).WithMessage(BundleProductStockExceededUnderlyingProductStockMessage)
                .When(MainProductIsManagedByBundleProducts);

            RuleForEach(BundleProducts).Where(UnderlyingProductAllowsBackordersAndIsManagedByAttributes).Cascade(CascadeMode.Stop)
                .Must(HaveNonNullCombination).WithMessage(CombinationNotExistsMessage)
                .Must(HaveBundleStockWhichDoesNotExceedProductCombinationStock).WithMessage(BundleProductStockExceededCombinationStockMessage).When(CombinationDoesNoAllowOutOfStockOrders, ApplyConditionTo.CurrentValidator)
                .When(MainProductIsManagedByBundleProducts);

            RuleFor(ProductCombination).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage(CombinationNotExistsMessage)
                .Must(HaveItemStockWhichDoesNotExceedProductCombinationStock).WithMessage(ItemStockExceededProductCombinationStockMessage).When(CombinationDoesNoAllowOutOfStockOrders, ApplyConditionTo.CurrentValidator)
                .When(MainProductIsManagedByAttributes);
        });
    }

    private static readonly Expression<Func<ShoppingCartInventoryProductContext, int>> ItemQuantity = x => x.ShoppingCartItem.Quantity;

    private static readonly Expression<Func<ShoppingCartInventoryProductContext, int>> ProductMinimumQuantity = x => x.Product.OrderMinimumQuantity;

    private static readonly Expression<Func<ShoppingCartInventoryProductContext, int>> ProductMaximumQuantity = x => x.Product.OrderMaximumQuantity;

    private static readonly Expression<Func<ShoppingCartInventoryProductContext, string>> WarehouseId = x => x.ShoppingCartItem.WarehouseId;

    private static readonly Expression<Func<ShoppingCartInventoryProductContext, int>> ProductQuantity = x => x.ShoppingCartItem.Quantity +
                                                                                                              x.Customer.ShoppingCartItems
                                                                                                                  .Where(sci => sci.ShoppingCartTypeId == x.ShoppingCartItem.ShoppingCartTypeId && sci.WarehouseId == x.ShoppingCartItem.WarehouseId &&
                                                                                                                                sci.ProductId == x.ShoppingCartItem.ProductId && sci.StoreId == x.ShoppingCartItem.StoreId &&
                                                                                                                                sci.Id != x.ShoppingCartItem.Id)
                                                                                                                  .Sum(sci => sci.Quantity);

    private static readonly Expression<Func<ShoppingCartInventoryProductContext, IEnumerable<BundleProductContext>>> BundleProducts = x => x.BundleProducts;

    private static readonly Expression<Func<ShoppingCartInventoryProductContext, ProductAttributeCombination>> ProductCombination = x => x.Product.FindProductAttributeCombination(x.ShoppingCartItem.Attributes, true);

    private static bool BeInTheListOfProductAllowedQuantities(ShoppingCartInventoryProductContext context, int quantity) => context.Product.ParseAllowedQuantities().Contains(quantity);

    private static bool AllowedProductQuantitiesIsNoEmpty(ShoppingCartInventoryProductContext context) => context.Product.ParseAllowedQuantities().Length > 0;

    private static bool AllowSelectWarehouse(ShoppingCartInventoryProductContext context) => context.AllowSelectWarehouse;

    private static bool ItemQuantityIsInValidRange(ShoppingCartInventoryProductContext context) =>
        context.ShoppingCartItem.Quantity >= context.Product.OrderMinimumQuantity && context.ShoppingCartItem.Quantity <= context.Product.OrderMaximumQuantity;

    private static bool BeLessThanOrEqualToTotalStock(ShoppingCartInventoryProductContext context, int quantity)
    {
        var warehouseId = !string.IsNullOrEmpty(context.ShoppingCartItem.WarehouseId) ? context.ShoppingCartItem.WarehouseId : string.Empty;
        var stockQuantity = context.StockQuantityService.GetTotalStockQuantity(context.Product, warehouseId: warehouseId);
        return quantity <= stockQuantity;
    }

    private static bool MainProductIsManagedByStockAndProductModeIsNoBackOrders(ShoppingCartInventoryProductContext context) =>
        context.Product.ManageInventoryMethodId == ManageInventoryMethod.ManageStock && context.Product.BackorderModeId == BackorderMode.NoBackorders;

    private static bool UnderlyingProductAllowsBackordersAndIsManagedByStock(BundleProductContext context) =>
        context.Product.BackorderModeId is not BackorderMode.NoBackorders && context.Product.ManageInventoryMethodId == ManageInventoryMethod.ManageStock;

    private static bool HaveBundleStockWhichDoesNotExceedUnderlyingProductStock(ShoppingCartInventoryProductContext context, BundleProductContext bundleProduct)
    {
        var quantity = context.ShoppingCartItem.Quantity * bundleProduct.BundleProduct.Quantity;
        var stockQuantity = context.StockQuantityService.GetTotalStockQuantity(bundleProduct.Product, warehouseId: context.ShoppingCartItem.WarehouseId);
        return quantity <= stockQuantity;
    }

    private static bool MainProductIsManagedByBundleProducts(ShoppingCartInventoryProductContext context) => context.Product.ManageInventoryMethodId == ManageInventoryMethod.ManageStockByBundleProducts;

    private static bool UnderlyingProductAllowsBackordersAndIsManagedByAttributes(BundleProductContext context) =>
        context.Product.BackorderModeId is not BackorderMode.NoBackorders && context.Product.ManageInventoryMethodId == ManageInventoryMethod.ManageStockByAttributes;

    private static bool HaveNonNullCombination(ShoppingCartInventoryProductContext context, BundleProductContext bundleProduct) => context.Product.FindProductAttributeCombination(context.ShoppingCartItem.Attributes) != null;

    private static bool HaveBundleStockWhichDoesNotExceedProductCombinationStock(ShoppingCartInventoryProductContext context, BundleProductContext bundleProduct)
    {
        var quantity = context.ShoppingCartItem.Quantity * bundleProduct.BundleProduct.Quantity;
        var stockQuantity = context.StockQuantityService.GetTotalStockQuantityForCombination(bundleProduct.Product, context.Product.FindProductAttributeCombination(context.ShoppingCartItem.Attributes),
            warehouseId: context.ShoppingCartItem.WarehouseId);
        return quantity <= stockQuantity;
    }

    private static bool CombinationDoesNoAllowOutOfStockOrders(ShoppingCartInventoryProductContext context) =>
        context.Product.FindProductAttributeCombination(context.ShoppingCartItem.Attributes).AllowOutOfStockOrders == false;

    private static bool HaveItemStockWhichDoesNotExceedProductCombinationStock(ShoppingCartInventoryProductContext context, ProductAttributeCombination combination)
    {
        var stockQuantity = context.StockQuantityService.GetTotalStockQuantityForCombination(context.Product, combination, warehouseId: context.ShoppingCartItem.WarehouseId);
        return context.ShoppingCartItem.Quantity <= stockQuantity;
    }

    private static bool MainProductIsManagedByAttributes(ShoppingCartInventoryProductContext context) => context.Product.ManageInventoryMethodId == ManageInventoryMethod.ManageStockByAttributes;

    private string MinimumQuantityMessage(ShoppingCartInventoryProductContext context, int quantity) =>
        string.Format(_translationService.GetResource("ShoppingCart.MinimumQuantity"), context.Product.Name, context.Product.OrderMinimumQuantity);

    private string MaximumQuantityMessage(ShoppingCartInventoryProductContext context, int quantity) =>
        string.Format(_translationService.GetResource("ShoppingCart.MaximumQuantity"), context.Product.Name, context.Product.OrderMaximumQuantity);

    private string AllowedQuantitiesMessage(ShoppingCartInventoryProductContext context, int quantity) =>
        string.Format(_translationService.GetResource("ShoppingCart.AllowedQuantities"), context.Product.Name, string.Join(", ", context.Product.ParseAllowedQuantities()));

    private string WarehouseRequiredMessage(ShoppingCartInventoryProductContext context, string warehouseId) =>
        _translationService.GetResource("ShoppingCart.RequiredWarehouse");

    private string StockExceededMessage(ShoppingCartInventoryProductContext context, int quantity)
    {
        return context.StockQuantityService.GetTotalStockQuantity(context.Product, warehouseId: context.ShoppingCartItem.WarehouseId) <= 0
            ? _translationService.GetResource("ShoppingCart.OutOfStock")
            : string.Format(_translationService.GetResource("ShoppingCart.QuantityExceedsStock"), context.StockQuantityService.GetTotalStockQuantity(context.Product, warehouseId: context.ShoppingCartItem.WarehouseId));
    }

    private string BundleProductStockExceededUnderlyingProductStockMessage(ShoppingCartInventoryProductContext context, BundleProductContext bundleProduct) =>
        string.Format(_translationService.GetResource("ShoppingCart.OutOfStock.BundleProduct"), bundleProduct.Product.Name);

    private string BundleProductStockExceededCombinationStockMessage(ShoppingCartInventoryProductContext context, BundleProductContext bundleProduct)
    {
        var stockQuantity = context.StockQuantityService.GetTotalStockQuantityForCombination(bundleProduct.Product, context.Product.FindProductAttributeCombination(context.ShoppingCartItem.Attributes), warehouseId: context.ShoppingCartItem.WarehouseId);
        return stockQuantity <= 0
            ? string.Format(_translationService.GetResource("ShoppingCart.OutOfStock.BundleProduct"), bundleProduct.Product.Name)
            : string.Format(_translationService.GetResource("ShoppingCart.QuantityExceedsStock.BundleProduct"), bundleProduct.Product.Name, stockQuantity);
    }

    private string CombinationNotExistsMessage(ShoppingCartInventoryProductContext context) => _translationService.GetResource("ShoppingCart.Combination.NotExist");

    private string ItemStockExceededProductCombinationStockMessage(ShoppingCartInventoryProductContext context, ProductAttributeCombination combination)
    {
        var stockQuantity = context.StockQuantityService.GetTotalStockQuantityForCombination(context.Product, combination, warehouseId: context.ShoppingCartItem.WarehouseId);
        return stockQuantity <= 0
            ? _translationService.GetResource("ShoppingCart.OutOfStock")
            : string.Format(_translationService.GetResource("ShoppingCart.QuantityExceedsStock"), stockQuantity);
    }
}