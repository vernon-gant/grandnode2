using FluentValidation;
using Grand.Domain.Payments;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of determining whether an offline payment can be partially applied, the system must ensure that:
/// 1. The payment transaction must be non-null.
/// 2. The transaction amount must not be zero when rule 1 holds true.
/// 3. The already paid amount must be less than the total transaction amount when rule 2 holds true.
/// 4. The target amount must be less than or equal to the remaining payable amount when rule 3 holds true.
/// 5. The transaction must be in one of the statuses PartialPaid, Pending, PartiallyRefunded, or Refunded when isRefund is false and rule 4 holds true.
/// 6. The transaction must be in one of the statuses Paid, PartialPaid, or PartiallyRefunded when isRefund is true and rule 4 holds true.
public record PartialOfflineTransactionEligibilityValidationContext(PaymentTransaction PaymentTransaction, double TargetAmount, bool IsRefund);

public class PartialOfflineTransactionEligibilityValidator : AbstractValidator<PartialOfflineTransactionEligibilityValidationContext>
{
    public PartialOfflineTransactionEligibilityValidator()
    {
        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .Must(HaveNonNullTransaction).Must(NotHaveTransactionAmountEqualToZero)
            .Must(HavePaidAmountLessThanTransactionAmount).Must(HaveTargetAmountLessOrEqualRemaining).Must(HaveValidTransactionStatus);
    }

    private static Expression<Func<PartialOfflineTransactionEligibilityValidationContext, PartialOfflineTransactionEligibilityValidationContext>> WholeContext => context => context;

    private static bool HaveNonNullTransaction(PartialOfflineTransactionEligibilityValidationContext context) => context.PaymentTransaction is not null;

    private static bool NotHaveTransactionAmountEqualToZero(PartialOfflineTransactionEligibilityValidationContext context) => context.PaymentTransaction.TransactionAmount != 0;

    private static bool HavePaidAmountLessThanTransactionAmount(PartialOfflineTransactionEligibilityValidationContext context) => context.PaymentTransaction.PaidAmount < context.PaymentTransaction.TransactionAmount;

    private static bool HaveTargetAmountLessOrEqualRemaining(PartialOfflineTransactionEligibilityValidationContext context) => context.TargetAmount <= context.PaymentTransaction.TransactionAmount - context.PaymentTransaction.PaidAmount;

    private static bool HaveValidTransactionStatus(PartialOfflineTransactionEligibilityValidationContext context)
    {
        if (context.IsRefund)
            return context.PaymentTransaction.TransactionStatus is TransactionStatus.Paid or TransactionStatus.PartialPaid or TransactionStatus.PartiallyRefunded;

        return context.PaymentTransaction.TransactionStatus is TransactionStatus.PartialPaid or TransactionStatus.Pending or TransactionStatus.PartiallyRefunded or TransactionStatus.Refunded;
    }
}