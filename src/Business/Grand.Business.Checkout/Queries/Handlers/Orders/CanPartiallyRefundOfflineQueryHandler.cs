using Grand.Business.Checkout.Validators;
using Grand.Business.Core.Queries.Checkout.Orders;
using MediatR;

namespace Grand.Business.Checkout.Queries.Handlers.Orders;

public class CanPartiallyRefundOfflineQueryHandler : IRequestHandler<CanPartiallyRefundOfflineQuery, bool>
{
    public Task<bool> Handle(CanPartiallyRefundOfflineQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new PartialOfflineTransactionEligibilityValidator().Validate(new PartialOfflineTransactionEligibilityValidationContext(request.PaymentTransaction, request.AmountToRefund, true)).IsValid);
    }
}