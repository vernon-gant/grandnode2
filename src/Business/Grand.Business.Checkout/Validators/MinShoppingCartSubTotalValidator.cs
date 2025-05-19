using FluentValidation;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Domain.Customers;
using Grand.Domain.Orders;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating the minimum shopping cart subtotal, the system must ensure that:
/// 1. The customer must be not null.
/// 2. The shopping cart must be not empty when rule 1 holds true.
/// 3. The shopping cart subtotal without discounts obtained using the order calculation service must be greater than or equal to the global minimum order subtotal when the global minimum order subtotal is greater than zero.
public record MinShoppingCartSubTotalValidationContext(OrderSettings OrderSettings, IOrderCalculationService OrderCalculationService, IReadOnlyList<ShoppingCartItem> Cart, Customer Customer);

public class MinShoppingCartSubTotalValidator : AbstractValidator<MinShoppingCartSubTotalValidationContext>
{
    public MinShoppingCartSubTotalValidator()
    {
        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .Must(HaveNonNullCustomer)
            .Must(HaveNotEmptyCart)
            .MustAsync(HaveCartSubTotalWithoutDiscountGreaterThanOrEqualToGlobalMinOrderSubtotal).When(GlobalMinOrderSubTotalIsGreaterThanZero, ApplyConditionTo.CurrentValidator);
    }

    private static Expression<Func<MinShoppingCartSubTotalValidationContext, MinShoppingCartSubTotalValidationContext>> WholeContext => context => context;

    private static bool HaveNonNullCustomer(MinShoppingCartSubTotalValidationContext context) => context.Customer is not null;

    private static bool HaveNotEmptyCart(MinShoppingCartSubTotalValidationContext context) => context.Cart is not null && context.Cart.Any();

    private static bool GlobalMinOrderSubTotalIsGreaterThanZero(MinShoppingCartSubTotalValidationContext context) => context.OrderSettings.MinOrderSubtotalAmount > 0;

    private static async Task<bool> HaveCartSubTotalWithoutDiscountGreaterThanOrEqualToGlobalMinOrderSubtotal(MinShoppingCartSubTotalValidationContext context, CancellationToken _)
    {
        var (_, _, subTotalWithoutDiscount, _, _) = await context.OrderCalculationService.GetShoppingCartSubTotal(context.Cart.ToList(), false);
        return subTotalWithoutDiscount >= context.OrderSettings.MinOrderSubtotalAmount;
    }
}