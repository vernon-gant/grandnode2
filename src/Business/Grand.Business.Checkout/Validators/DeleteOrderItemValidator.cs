using FluentValidation;
using Grand.Domain.Catalog;
using Grand.Domain.Orders;
using Grand.Domain.Shipping;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating the deletion of an order item, the system must ensure that:
/// 1. The product must not be null.
/// 2. The open quantity of the order item must not be zero when rule 1 holds true.
/// 3. The order item status must not be "Close" when rule 2 holds true.
/// 4. The open quantity of the order item must be equal to the order item quantity when rule 3 holds true.
/// 5. The gift vouchers list must not be empty when the product is a gift voucher and rule 4 holds true.
/// 6. The gift vouchers list must be empty when product is not a gift voucher and rule 4 holds true.
/// 6. The shipments must not contain any order items when rule 5 holds true.
/// 7. The order item must not be associated with any gift voucher record using the gift voucher service when rule 6 holds true.
public record DeleteOrderItemValidationContext(OrderItem OrderItem, Product Product, IReadOnlyList<GiftVoucher> GiftVouchers, IReadOnlyList<Shipment> Shipments);

public class DeleteOrderItemValidator : AbstractValidator<DeleteOrderItemValidationContext>
{
    public DeleteOrderItemValidator()
    {
        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .Must(HaveNotNullProduct).WithMessage("Product not exists.")
            .Must(HaveOrderItemWithOpenQuantityNotZero).WithMessage("You can't delete this order item.")
            .Must(HaveOrderItemWithNonCloseStatus).WithMessage("You can't delete this order item.")
            .Must(HaveOrderItemWithOpenQuantityEqualToQuantity).WithMessage("You can't delete this order item.")
            .Must(HaveGiftVouchersWhichAreNotEmpty).WithMessage("You can't delete item with gift voucher, first go to gift vouchers and delete it").When(ProductIsGiftVoucher, ApplyConditionTo.CurrentValidator)
            .Must(HaveGiftVouchersWhichAreEmpty).WithMessage("You can't delete item with gift voucher, first go to gift vouchers and delete it").When(ProductIsNotGiftVoucher, ApplyConditionTo.CurrentValidator)
            .Must(HaveShipmentsWhichDoNotContainOrderItems).WithMessage("You can't delete item with shipment, first go to shipment and delete it");
    }

    private static readonly Expression<Func<DeleteOrderItemValidationContext, DeleteOrderItemValidationContext>> WholeContext = context => context;

    private static bool HaveNotNullProduct(DeleteOrderItemValidationContext context) => context.Product != null;

    private static bool HaveOrderItemWithOpenQuantityNotZero(DeleteOrderItemValidationContext context) => context.OrderItem.OpenQty != 0;

    private static bool HaveOrderItemWithNonCloseStatus(DeleteOrderItemValidationContext context) => context.OrderItem.Status != OrderItemStatus.Close;

    private static bool HaveOrderItemWithOpenQuantityEqualToQuantity(DeleteOrderItemValidationContext context) => context.OrderItem.OpenQty == context.OrderItem.Quantity;

    private static bool HaveGiftVouchersWhichAreNotEmpty(DeleteOrderItemValidationContext context) => context.GiftVouchers.Any();

    private static bool HaveGiftVouchersWhichAreEmpty(DeleteOrderItemValidationContext context) => !context.GiftVouchers.Any();

    private static bool HaveShipmentsWhichDoNotContainOrderItems(DeleteOrderItemValidationContext context) => !context.Shipments.Any();

    private static bool ProductIsGiftVoucher(DeleteOrderItemValidationContext context) => context.Product.IsGiftVoucher;

    private static bool ProductIsNotGiftVoucher(DeleteOrderItemValidationContext context) => !context.Product.IsGiftVoucher;
}