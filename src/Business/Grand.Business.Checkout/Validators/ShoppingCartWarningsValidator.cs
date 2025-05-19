using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating consistency in a set of products in the shopping cart, the system must ensure that:
/// 1. The product must not be null.
/// 2. The standard products and recurring products must not be mixed.
/// 3. The recurring products must share the same cycle period, cycle length, and total cycles.
public record ShoppingCartWarningsValidationContext(IReadOnlyList<Product> Products);

public class ShoppingCartWarningsValidator : AbstractValidator<ShoppingCartWarningsValidationContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartWarningsValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleForEach(ProductsEnumerable)
            .NotNull().WithMessage(CouldNotLoadProductMessage)
            .DependentRules(() =>
            {
                RuleFor(ProductsList)
                    .Must(NotMixStandardAndRecurring).WithMessage(CannotMixStandardAndAutoshipProductsMessage)
                    .Must(ShareSameCycleSettings).WithMessage(CannotMixRecurringProductsMessage);
            });
    }

    private static readonly Expression<Func<ShoppingCartWarningsValidationContext, IEnumerable<Product>>> ProductsEnumerable = context => context.Products;

    private static readonly Expression<Func<ShoppingCartWarningsValidationContext, IReadOnlyList<Product>>> ProductsList = context => context.Products;

    private static bool NotMixStandardAndRecurring(IReadOnlyList<Product> products) => products.All(product => product.IsRecurring) || products.All(product => !product.IsRecurring);

    private static bool ShareSameCycleSettings(IReadOnlyList<Product> products)
    {
        var firstProduct = products.FirstOrDefault();
        return products.All(product => product.RecurringCyclePeriodId == firstProduct!.RecurringCyclePeriodId &&
                                       product.RecurringCycleLength == firstProduct.RecurringCycleLength &&
                                       product.RecurringTotalCycles == firstProduct.RecurringTotalCycles);
    }

    private string CouldNotLoadProductMessage(ShoppingCartWarningsValidationContext context, Product product) => _translationService.GetResource("ShoppingCart.CannotLoadProduct", product.Id);

    private string CannotMixStandardAndAutoshipProductsMessage => _translationService.GetResource("ShoppingCart.CannotMixStandardAndAutoshipProducts");

    private string CannotMixRecurringProductsMessage => _translationService.GetResource("ShoppingCart.CannotMixRecurringProducts");

}