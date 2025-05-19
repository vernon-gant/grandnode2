using Grand.Business.Checkout.Validators;
using Grand.Business.Core.Commands.Checkout.Orders;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Business.Core.Interfaces.Checkout.GiftVouchers;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Business.Core.Interfaces.Checkout.Shipping;
using Grand.Domain.Orders;
using Grand.Domain.Shipping;
using MediatR;

namespace Grand.Business.Checkout.Commands.Handlers.Orders;

public class DeleteOrderItemCommandHandler : IRequestHandler<DeleteOrderItemCommand, (bool error, string message)>
{
    private readonly IGiftVoucherService _giftVoucherService;
    private readonly IInventoryManageService _inventoryManageService;
    private readonly IMediator _mediator;
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly IShipmentService _shipmentService;

    public DeleteOrderItemCommandHandler(
        IMediator mediator,
        IOrderService orderService,
        IShipmentService shipmentService,
        IProductService productService,
        IGiftVoucherService giftVoucherService,
        IInventoryManageService inventoryManageService)
    {
        _mediator = mediator;
        _orderService = orderService;
        _shipmentService = shipmentService;
        _productService = productService;
        _giftVoucherService = giftVoucherService;
        _inventoryManageService = inventoryManageService;
    }

    public async Task<(bool error, string message)> Handle(DeleteOrderItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Order);
        ArgumentNullException.ThrowIfNull(request.OrderItem);

        var product = await _productService.GetProductById(request.OrderItem.ProductId);
        var shipments = await _shipmentService.GetShipmentsByOrder(request.Order.Id);
        var giftVouchers = await _giftVoucherService.GetGiftVouchersByPurchasedWithOrderItemId(request.OrderItem.Id);
        var validationResult = new DeleteOrderItemValidator().Validate(new DeleteOrderItemValidationContext(request.OrderItem, product, giftVouchers.AsReadOnly(), shipments.AsReadOnly()));

        if (!validationResult.IsValid)
            return (true, validationResult.Errors.First().ErrorMessage);

        //add a note
        await _orderService.InsertOrderNote(new OrderNote {
            Note = $"Order item has been deleted - {product.Name}",
            DisplayToCustomer = false,
            OrderId = request.Order.Id
        });

        await _inventoryManageService.AdjustReserved(product, request.OrderItem.Quantity, request.OrderItem.Attributes,
            request.OrderItem.WarehouseId);

        //delete item
        request.Order.OrderItems.Remove(request.OrderItem);

        request.Order.OrderSubtotalExclTax -= request.OrderItem.PriceExclTax;
        request.Order.OrderSubtotalInclTax -= request.OrderItem.PriceInclTax;
        request.Order.OrderTax -= request.OrderItem.PriceInclTax - request.OrderItem.PriceExclTax;
        request.Order.OrderTotal -= request.OrderItem.PriceInclTax;

        if (request.Order.ShippingStatusId == ShippingStatus.PartiallyShipped)
        {
            if (!request.Order.HasItemsToAddToShipment() && shipments.All(x => x.DeliveryDateUtc != null))
                request.Order.ShippingStatusId = ShippingStatus.Delivered;
            if (!request.Order.HasItemsToAddToShipment() && shipments.All(x => x.ShippedDateUtc != null))
                request.Order.ShippingStatusId = ShippingStatus.Shipped;
        }

        //TODO
        //request.Order.OrderTaxes

        await _orderService.UpdateOrder(request.Order);

        //check order status
        await _mediator.Send(new CheckOrderStatusCommand { Order = request.Order }, cancellationToken);

        return (false, "");
    }
}