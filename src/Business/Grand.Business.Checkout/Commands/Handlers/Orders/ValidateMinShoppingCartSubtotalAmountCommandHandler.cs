using Grand.Business.Checkout.Validators;
using Grand.Business.Core.Commands.Checkout.Orders;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Domain.Orders;
using MediatR;

namespace Grand.Business.Checkout.Commands.Handlers.Orders;

public class ValidateMinShoppingCartSubtotalAmountCommandHandler : IRequestHandler<ValidateMinShoppingCartSubtotalAmountCommand, bool>
{
    private readonly OrderSettings _orderSettings;
    private readonly IOrderCalculationService _orderTotalCalculationService;

    public ValidateMinShoppingCartSubtotalAmountCommandHandler(
        IOrderCalculationService orderTotalCalculationService,
        OrderSettings orderSettings)
    {
        _orderTotalCalculationService = orderTotalCalculationService;
        _orderSettings = orderSettings;
    }

    public async Task<bool> Handle(ValidateMinShoppingCartSubtotalAmountCommand request, CancellationToken cancellationToken)
    {
        var validationContext = new MinShoppingCartSubTotalValidationContext(_orderSettings, _orderTotalCalculationService, request.Cart.AsReadOnly(), request.Customer);
        var result = await new MinShoppingCartSubTotalValidator().ValidateAsync(validationContext, cancellationToken);
        return result.IsValid;
    }
}