using Grand.Business.Checkout.Validators;
using Grand.Business.Core.Queries.Checkout.Orders;
using MediatR;

namespace Grand.Business.Checkout.Queries.Handlers.Orders;

public class CanPartiallyPaidOfflineQueryHandler : IRequestHandler<CanPartiallyPaidOfflineQuery, bool>
{
    public Task<bool> Handle(CanPartiallyPaidOfflineQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new PartialOfflineTransactionEligibilityValidator().Validate(new PartialOfflineTransactionEligibilityValidationContext(request.PaymentTransaction, request.AmountToPaid, false)).IsValid);
    }
}