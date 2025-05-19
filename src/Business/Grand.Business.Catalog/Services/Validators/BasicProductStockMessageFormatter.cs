using FluentValidation;
using Grand.Domain.Catalog;
using System.Linq.Expressions;

namespace Grand.Business.Catalog.Services.Validators;

/// In the context of formatting a product stock state message, the system must ensure that:
/// 1. The validator must return in stock with quantity message when the stock quantity is greater than 0 and product has display stock quantity set to true.
/// 2. The validator must return in stock message when the stock quantity is greater than 0 and product has display stock quantity set to false.
/// 3. The validator must return out of stock message when backorder mode is NoBackorders and stock quantity is equal or less than 0
/// 4. The validator must return back ordering message when backorder mode is AllowQtyBelow0 and stock quantity is equal or less than 0
public record BasicProductStockMessageFormattingContext(int StockQuantity, Product Product);

public class BasicProductStockMessageFormatter : AbstractValidator<BasicProductStockMessageFormattingContext>
{
    public BasicProductStockMessageFormatter()
    {
        When(StockQuantityIsGreaterThanZero, () =>
        {
            RuleFor(WholeContext)
                .Must(ReturnMessage).WithMessage("Products.Availability.InStockWithQuantity").When(StockQuantityMustBeDisplayed).WithState(StockQuantity)
                .Must(ReturnMessage).WithMessage("Products.Availability.InStock").When(StockQuantityMustNotBeDisplayed, ApplyConditionTo.CurrentValidator);
        }).Otherwise(() =>
        {
            RuleFor(WholeContext)
                .Must(ReturnMessage).WithMessage("Products.Availability.OutOfStock").When(BackOrderModeIsNoBackorders)
                .Must(ReturnMessage).WithMessage("Products.Availability.Backordering").When(BackOrderModeIsAllowQtyBelow0);
        });
    }

    private static readonly Expression<Func<BasicProductStockMessageFormattingContext, BasicProductStockMessageFormattingContext>> WholeContext = context => context;

    private static object StockQuantity(BasicProductStockMessageFormattingContext context) => context.StockQuantity;

    private static bool ReturnMessage(BasicProductStockMessageFormattingContext context) => false;

    private static bool StockQuantityIsGreaterThanZero(BasicProductStockMessageFormattingContext context) => context.StockQuantity > 0;

    private static bool StockQuantityMustBeDisplayed(BasicProductStockMessageFormattingContext context) => context.Product.DisplayStockQuantity;

    private static bool StockQuantityMustNotBeDisplayed(BasicProductStockMessageFormattingContext context) => !context.Product.DisplayStockQuantity;

    private static bool BackOrderModeIsNoBackorders(BasicProductStockMessageFormattingContext context) => context.Product.BackorderModeId == BackorderMode.NoBackorders;

    private static bool BackOrderModeIsAllowQtyBelow0(BasicProductStockMessageFormattingContext context) => context.Product.BackorderModeId == BackorderMode.AllowQtyBelowZero;
}