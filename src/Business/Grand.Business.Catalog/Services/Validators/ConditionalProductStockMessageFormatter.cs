using FluentValidation;
using Grand.Domain.Catalog;
using System.Linq.Expressions;

namespace Grand.Business.Catalog.Services.Validators;

/// In the context of formatting a product stock state message for different inventory methods, the system must ensure that:
/// 1. The validator must compute the stock quantity differently based on the inventory method id.
/// 2. The validator must return the combination does not exist message when the product combination is null and inventory method is ManageStockByAttributes.
/// 3. The validator must not execute the base class validation when the product combination is null and inventory method is ManageStockByAttributes.
/// 4. The validator must return empty stock message when the method inventory method is neither ManageStock nor ManageStockByAttributes.
public record ConditionalProductStockMessageFormattingContext(
    Product Product,
    ManageInventoryMethod ManageInventoryMethodId,
    Func<Product, ProductAttributeCombination, bool, string, int> GetStockForCombination,
    Func<Product, bool, string, bool, int> GetStockForProduct,
    string WarehouseId,
    ProductAttributeCombination ProductAttributeCombination);

public class ConditionalProductStockMessageFormatter : AbstractValidator<ConditionalProductStockMessageFormattingContext>
{
    public ConditionalProductStockMessageFormatter(IValidator<BasicProductStockMessageFormattingContext> validator)
    {
        When(StockIsAvailable, () =>
        {
            RuleFor(WholeContext).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Products.Availability.CombinationDoesNotExist")
                .DependentRules(() =>
                {
                    RuleFor(BasisValidationContextWithStockForCombination).SetValidator(validator).OverridePropertyName("Context");
                })
                .When(InventoryMethodIsManageStockByAttributes);

            RuleFor(BasisValidationContextWithNormalStockComputation).SetValidator(validator).OverridePropertyName("Context").When(InventoryMethodIsManageStock);

            RuleFor(WholeContext).Must(ReturnMessage).When(AnyOtherInventoryMethod);
        }).Otherwise(() =>
        {
            RuleFor(WholeContext).Must(ReturnMessage);
        });
    }

    private static readonly Expression<Func<ConditionalProductStockMessageFormattingContext, ConditionalProductStockMessageFormattingContext>> WholeContext = context => context;

    private static Expression<Func<ConditionalProductStockMessageFormattingContext, BasicProductStockMessageFormattingContext>> BasisValidationContextWithStockForCombination => context =>
        new BasicProductStockMessageFormattingContext(context.GetStockForCombination(context.Product, context.ProductAttributeCombination, true, context.WarehouseId), context.Product);

    private static Expression<Func<ConditionalProductStockMessageFormattingContext, BasicProductStockMessageFormattingContext>> BasisValidationContextWithNormalStockComputation =>
        context => new BasicProductStockMessageFormattingContext(context.GetStockForProduct(context.Product, true, context.WarehouseId, false), context.Product);

    private static bool StockIsAvailable(ConditionalProductStockMessageFormattingContext context) => context.Product.StockAvailability;

    private static bool ReturnMessage(ConditionalProductStockMessageFormattingContext context) => false;

    private static bool InventoryMethodIsManageStock(ConditionalProductStockMessageFormattingContext context) => context.ManageInventoryMethodId == ManageInventoryMethod.ManageStock;
    private static bool InventoryMethodIsManageStockByAttributes(ConditionalProductStockMessageFormattingContext context) => context.ManageInventoryMethodId == ManageInventoryMethod.ManageStockByAttributes;

    private static bool AnyOtherInventoryMethod(ConditionalProductStockMessageFormattingContext context) => !InventoryMethodIsManageStock(context) && !InventoryMethodIsManageStockByAttributes(context);
}