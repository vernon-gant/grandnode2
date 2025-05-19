using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using Grand.Domain.Orders;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating required products in a shopping cart, the system must ensure that:
/// 1. All required products must be present in the customer's shopping cart.
public record ShoppingCartRequiredProductsValidationContext(IReadOnlyList<ShoppingCartItem> CustomerShoppingCart, IReadOnlyList<Product> RequiredProducts);

public record ProductWithCart(Product Product, IReadOnlyList<ShoppingCartItem> CustomerShoppingCart);

public class ShoppingCartRequiredProductValidator : AbstractValidator<ShoppingCartRequiredProductsValidationContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartRequiredProductValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleForEach(RequiredProducts).Where(NotInCart).Must(ReturnMessage).WithMessage(MissingRequiredProductErrorMessage).OverridePropertyName("RequiredProducts");
    }

    private static readonly Expression<Func<ShoppingCartRequiredProductsValidationContext, IEnumerable<ProductWithCart>>> RequiredProducts = x => x.RequiredProducts.Select(product => new ProductWithCart(product, x.CustomerShoppingCart));

    private static bool NotInCart(ProductWithCart product) => product.CustomerShoppingCart.All(sci => sci.ProductId != product.Product.Id);

    private static bool ReturnMessage(ProductWithCart product) => false;

    private string MissingRequiredProductErrorMessage(ShoppingCartRequiredProductsValidationContext validationContext, ProductWithCart product) => string.Format(_translationService.GetResource("ShoppingCart.RequiredProductWarning"), product.Product.Name);
}