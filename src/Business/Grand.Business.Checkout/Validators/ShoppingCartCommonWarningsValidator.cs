using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Domain.Customers;
using Grand.Domain.Orders;
using Grand.Domain.Permissions;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

// In the context of validating common warnings for shopping cart and wishlist, the system must ensure that:
// 1. The number of items in the shopping cart must be less than the maximum allowed items for shopping cart in global settings when the shopping cart type is ShoppingCart.
// 2. The number of items in the shopping cart must be less than the maximum allowed items for wishlist in global settings when the shopping cart type is Wishlist.
// 3. The customer must be authorized by permission service to enable the shopping cart when the shopping cart type is ShoppingCart.
// 4. The customer must be authorized by permission service to enable the wishlist when the shopping cart type is Wishlist.
// 5. The requested quantity must be greater than 0.
public record ShoppingCartCommonWarningsValidationContext(Customer Customer, IReadOnlyList<ShoppingCartItem> ShoppingCarts, ShoppingCartType ShoppingCartType, int RequestedQuantity, ShoppingCartSettings Settings, IPermissionService PermissionService);

public class ShoppingCartCommonWarningsValidator : AbstractValidator<ShoppingCartCommonWarningsValidationContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartCommonWarningsValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleFor(ItemCount)
            .LessThan(MaxCartItems).When(ShoppingCartTypeIsShoppingCart, ApplyConditionTo.CurrentValidator).WithMessage(MaxCartItemsMessage)
            .LessThan(MaxWishlistItems).When(ShoppingCartTypeIsWishlist, ApplyConditionTo.CurrentValidator).WithMessage(MaxWishlistItemsMessage);

        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .MustAsync(HaveCustomerAuthorizedForCartEnabling).When(ShoppingCartTypeIsShoppingCart, ApplyConditionTo.CurrentValidator).WithMessage(CartDisabledMessage)
            .MustAsync(HaveCustomerAuthorizedForWishlistEnabling).When(ShoppingCartTypeIsWishlist, ApplyConditionTo.CurrentValidator).WithMessage(WishlistDisabledMessage);

        RuleFor(Quantity).GreaterThan(0).WithMessage(QuantityPositiveMessage);
    }

    private static readonly Expression<Func<ShoppingCartCommonWarningsValidationContext, int>> ItemCount = context => context.ShoppingCarts.Count;

    private static readonly Expression<Func<ShoppingCartCommonWarningsValidationContext, ShoppingCartCommonWarningsValidationContext>> WholeContext = context => context;

    private static readonly Expression<Func<ShoppingCartCommonWarningsValidationContext, int>> Quantity = context => context.RequestedQuantity;

    private static readonly Expression<Func<ShoppingCartCommonWarningsValidationContext, int>> MaxCartItems = context => context.Settings.MaximumShoppingCartItems;

    private static readonly Expression<Func<ShoppingCartCommonWarningsValidationContext, int>> MaxWishlistItems = context => context.Settings.MaximumWishlistItems;

    private static bool ShoppingCartTypeIsShoppingCart(ShoppingCartCommonWarningsValidationContext validationContext) => validationContext.ShoppingCartType == ShoppingCartType.ShoppingCart;

    private static bool ShoppingCartTypeIsWishlist(ShoppingCartCommonWarningsValidationContext validationContext) => validationContext.ShoppingCartType == ShoppingCartType.Wishlist;

    private async Task<bool> HaveCustomerAuthorizedForCartEnabling(ShoppingCartCommonWarningsValidationContext validationContext, CancellationToken cancellationToken) => await validationContext.PermissionService.Authorize(StandardPermission.EnableShoppingCart, validationContext.Customer);

    private async Task<bool> HaveCustomerAuthorizedForWishlistEnabling(ShoppingCartCommonWarningsValidationContext validationContext, CancellationToken cancellationToken) => await validationContext.PermissionService.Authorize(StandardPermission.EnableWishlist, validationContext.Customer);

    private string MaxCartItemsMessage(ShoppingCartCommonWarningsValidationContext validationContext) => string.Format(_translationService.GetResource("ShoppingCart.MaximumShoppingCartItems"), validationContext.Settings.MaximumShoppingCartItems);

    private string MaxWishlistItemsMessage(ShoppingCartCommonWarningsValidationContext validationContext) => string.Format(_translationService.GetResource("ShoppingCart.MaximumWishlistItems"), validationContext.Settings.MaximumWishlistItems);

    private string CartDisabledMessage(ShoppingCartCommonWarningsValidationContext validationContext) => _translationService.GetResource("ShoppingCart.CartDisabled");

    private string WishlistDisabledMessage(ShoppingCartCommonWarningsValidationContext validationContext) => _translationService.GetResource("ShoppingCart.WishlistDisabled");

    private string QuantityPositiveMessage(ShoppingCartCommonWarningsValidationContext validationContext) => _translationService.GetResource("ShoppingCart.QuantityShouldPositive");
}