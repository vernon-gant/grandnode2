using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Domain.Catalog;
using Grand.Domain.Customers;
using Grand.Domain.Orders;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of adding a standard product to the shopping cart or wishlist, the system must ensure that:
/// 1. The product must be published.
/// 2. The product must be not a grouped product.
/// 3. The customer must be authorized by acl service to see the product.
/// 4. The store must be authorized by acl service to show the product.
/// 5. The product must have enabled buy button when adding to the shopping cart.
/// 6. The product must have enabled wishlist button when adding to the wishlist.
/// 7. The product must not be call for price when adding to the shopping cart.
/// 8. The shopping cart's item entered price must be within the product min entered price and max entered price when product is entered by price.
/// 9. The product’s available start date must be in the past when available start date is specified.
/// 10. The product’s available end date must be in the future when available end date is specified, product is added to the shopping cart and rule 9 holds true.
public record ShoppingCartStandardValidationContext(Customer Customer, Product Product, IAclService AclService, ShoppingCartItem ShoppingCartItem);

public class ShoppingCartStandardValidator : AbstractValidator<ShoppingCartStandardValidationContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartStandardValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleFor(ProductIsPublished).Equal(true).WithMessage(ProductUnpublishedMessage);

        RuleFor(ProductTypeId).NotEqual(ProductType.GroupedProduct).WithMessage(GroupedProductErrorMessage);

        RuleFor(Customer).Must(BeAuthorizedToSeeTheProduct).WithMessage(ProductUnpublishedMessage);

        RuleFor(Store).Must(BeAuthorizedToShowTheProduct).WithMessage(ProductUnpublishedMessage);

        RuleFor(ProductButtonIsDisabled).Equal(false).When(AddingToShoppingCart).WithMessage(BuyingDisabledMessage);

        RuleFor(ProductWishlistButtonIsDisabled).Equal(false).When(AddingToWishlist).WithMessage(WishlistDisabledMessage);

        RuleFor(ProductIsCallForPrice).Equal(false).When(AddingToShoppingCart).WithMessage(CallForPriceMessage);

        RuleFor(ShoppingCartItemEnteredPrice).Must(BeWithinTheProductMinAndMaxEnteredPrice).When(ProductIsEnteredByPrice).WithMessage(EnteredPriceRangeMessage);

        RuleFor(ProductAvailableStartDateTimeUtc).LessThanOrEqualTo(Now).When(ProductAvailableStartDateTimeUtcIsSpecified).WithMessage(NotAvailableMessage);

        RuleFor(ProductAvailableEndDateTimeUtc).GreaterThan(Now).When(ProductAvailableEndDateIsSetAndAddingToShoppingCart).WithMessage(NotAvailableMessage);
    }

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, bool>> ProductIsPublished = x => x.Product.Published;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, ProductType>> ProductTypeId = x => x.Product.ProductTypeId;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, Customer>> Customer = x => x.Customer;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, string>> Store = x => x.ShoppingCartItem.StoreId;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, bool>> ProductButtonIsDisabled = x => x.Product.DisableBuyButton;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, bool>> ProductWishlistButtonIsDisabled = x => x.Product.DisableWishlistButton;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, bool>> ProductIsCallForPrice = x => x.Product.CallForPrice;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, double>> ShoppingCartItemEnteredPrice = x => x.ShoppingCartItem.EnteredPrice ?? 0;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, DateTime?>> ProductAvailableStartDateTimeUtc = x => x.Product.AvailableStartDateTimeUtc;

    private static readonly Expression<Func<ShoppingCartStandardValidationContext, DateTime?>> ProductAvailableEndDateTimeUtc = x => x.Product.AvailableEndDateTimeUtc;

    private static bool BeAuthorizedToSeeTheProduct(ShoppingCartStandardValidationContext validationContext, Customer customer) => validationContext.AclService.Authorize(validationContext.Product, customer);

    private static bool BeAuthorizedToShowTheProduct(ShoppingCartStandardValidationContext validationContext, string storeId) => validationContext.AclService.Authorize(validationContext.Product, storeId);

    private static bool BeWithinTheProductMinAndMaxEnteredPrice(ShoppingCartStandardValidationContext validationContext, double shoppingCartItemEnteredPrice) =>
        shoppingCartItemEnteredPrice >= validationContext.Product.MinEnteredPrice && shoppingCartItemEnteredPrice <= validationContext.Product.MaxEnteredPrice;

    private static bool ProductAvailableStartDateTimeUtcIsSpecified(ShoppingCartStandardValidationContext validationContext) => validationContext.Product.AvailableStartDateTimeUtc.HasValue;

    private static bool ProductAvailableEndDateIsSetAndAddingToShoppingCart(ShoppingCartStandardValidationContext validationContext) =>
        validationContext.Product.AvailableEndDateTimeUtc.HasValue && validationContext.ShoppingCartItem.ShoppingCartTypeId == ShoppingCartType.ShoppingCart;

    private static bool AddingToShoppingCart(ShoppingCartStandardValidationContext validationContext) => validationContext.ShoppingCartItem.ShoppingCartTypeId == ShoppingCartType.ShoppingCart;

    private static bool AddingToWishlist(ShoppingCartStandardValidationContext validationContext) => validationContext.ShoppingCartItem.ShoppingCartTypeId == ShoppingCartType.Wishlist;

    private static bool ProductIsEnteredByPrice(ShoppingCartStandardValidationContext validationContext) => validationContext.Product.EnteredPrice;

    private static DateTime Now => DateTime.UtcNow;

    private string ProductUnpublishedMessage => _translationService.GetResource("ShoppingCart.ProductUnpublished");

    private const string GroupedProductErrorMessage = "You can't add grouped value.Product";

    private string BuyingDisabledMessage => _translationService.GetResource("ShoppingCart.BuyingDisabled");

    private string WishlistDisabledMessage => _translationService.GetResource("ShoppingCart.WishlistDisabled");

    private string CallForPriceMessage => _translationService.GetResource("Products.CallForPrice");

    private string EnteredPriceRangeMessage => _translationService.GetResource("ShoppingCart.CustomerEnteredPrice.RangeError");

    private string NotAvailableMessage => _translationService.GetResource("ShoppingCart.NotAvailable");
}