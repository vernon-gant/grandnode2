using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of placing an auction bid during shopping cart checkout, the system must ensure that:
/// 1. The provided bid must be greater than both product highest bid and product start price
/// 2. The available product end date time must not be null
/// 3. The available product end date time must be later than the current UTC time when rule 2 holds true
public record ShoppingCartAuctionContext(Product Product, double Bid);

public class ShoppingCartAuctionValidator : AbstractValidator<ShoppingCartAuctionContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartAuctionValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleFor(Bid).GreaterThan(HighestBid).GreaterThanOrEqualTo(HighestPrice).WithMessage(BidMustBeHigherMessage);

        RuleFor(EndDate).Cascade(CascadeMode.Stop).NotNull().WithMessage(NotAvailableMessage).GreaterThanOrEqualTo(DateTime.UtcNow).WithMessage(NotAvailableMessage);
    }

    private static readonly Expression<Func<ShoppingCartAuctionContext, double>> Bid = context => context.Bid;

    private static readonly Expression<Func<ShoppingCartAuctionContext, double>> HighestBid = context => context.Product.HighestBid;

    private static readonly Expression<Func<ShoppingCartAuctionContext, double>> HighestPrice = context => context.Product.StartPrice;

    private static readonly Expression<Func<ShoppingCartAuctionContext, DateTime?>> EndDate = ctx => ctx.Product.AvailableEndDateTimeUtc;

    private string BidMustBeHigherMessage => _translationService.GetResource("ShoppingCart.Auction.BidMustBeHigher");

    private string NotAvailableMessage => _translationService.GetResource("ShoppingCart.Auction.NotAvailable");
}