using FluentValidation;
using Grand.Domain.Catalog;
using Grand.Domain.Orders;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating order item cancellation eligibility, the system must ensure that:
/// 1. The order must not be null.
/// 2. The order quantity must be greater than 0 when rule 1 holds true.
/// 3. The order item status must noy be Close when rule 2 holds true.
/// 4. The product must not be a gift voucher when rule 3 holds true.
public record CancelOrderItemValidationContext(Product Product, int OrderQuantity, OrderItemStatus Status);

public class CancelOrderItemValidator : AbstractValidator<CancelOrderItemValidationContext>
{
    public CancelOrderItemValidator()
    {
        RuleFor(WholeContext)
            .Must(HaveNonNullProduct).WithMessage("Product not exists.")
            .Must(HaveOderQuantityBiggerThanZero).WithMessage("You can't cancel this order item.")
            .Must(HaveOrderItemStatusNotClose).WithMessage("You can't cancel this order item.")
            .Must(HaveProductNotGiftVoucher).WithMessage("You can't cancel gift voucher, please delete it.");
    }

    private static readonly Expression<Func<CancelOrderItemValidationContext, CancelOrderItemValidationContext>> WholeContext = context => context;

    private static bool HaveNonNullProduct(CancelOrderItemValidationContext context) => context.Product != null;

    private static bool HaveOderQuantityBiggerThanZero(CancelOrderItemValidationContext context) => context.OrderQuantity > 0;

    private static bool HaveOrderItemStatusNotClose(CancelOrderItemValidationContext context) => context.Status != OrderItemStatus.Close;

    private static bool HaveProductNotGiftVoucher(CancelOrderItemValidationContext context) => !context.Product.IsGiftVoucher;
}