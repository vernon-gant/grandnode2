using FluentValidation;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using Grand.Domain.Customers;
using Grand.Domain.Orders;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating a shopping cart reservation product, the system must ensure that:
/// 1. The customer must not have a reservation obtained from the product reservation service that was already done by them.
/// 2. The shopping cart item reservation ID must not be null when the product interval unit is not a day.
/// 3. The requested reservation period must be valid when the shopping cart item has a start and end rental date.
/// 4. The product reservation must not be null when the interval unit is not a day.
/// 5. The product reservation order ID must not be null when the interval unit is not a day and rule 4 holds true.
/// 7. The shopping cart item start and end rental dates must be set when the interval unit is day.
/// 8. The shopping cart item start rental date must be less than or equal to the end rental date when both dates are included and rule 7 holds true.
/// 9. The shopping cart item start rental date must be less than the end rental date when both dates are not included and rule 7 holds true.
/// 10. The shopping cart item start rental date must be greater than or equal to the current date when rule 7 holds true.
/// 11. The shopping cart item end rental date must be greater than or equal to the current date when rule 7 holds true.
/// 12. The customer must have at least one reservation when the shopping cart item is already in the shopping cart.
/// 13. The customer reservation must not be null when the shopping cart item is already in the shopping cart and rule 12 holds true.
/// 14. The customer reservation order ID must be null when the shopping cart item is already in the shopping cart and rule 12 holds true.
public record ShoppingCartReservationProductValidationContext(Customer Customer, Product Product, ShoppingCartItem ShoppingCartItem, IProductReservationService ProductReservationService);

public class ShoppingCartReservationProductValidator : AbstractValidator<ShoppingCartReservationProductValidationContext>
{
    private readonly ITranslationService _translationService;
    private ProductReservation _productReservation;
    private IList<CustomerReservationsHelper> _customerReservations;
    private readonly Dictionary<CustomerReservationsHelper, ProductReservation> _customerReservationToProductReservation = new();

    public ShoppingCartReservationProductValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleFor(WholeContext).MustAsync(NotHaveReservationWhichWasAlreadyDoneByCustomer).When(ReservationIdIsProvidedAndItemNotInCart).WithMessage(AlreadyReservationMessage).OverridePropertyName("Reservation");

        RuleFor(ReservationId).NotNull().When(IntervalUnitIsNotDay).WithMessage(NoReservationFoundMessage).OverridePropertyName("Reservation");

        RuleFor(WholeContext).MustAsync(HaveValidRequestedReservationPeriod).When(CartItemHasStartAndEndRentalDate).WithMessage(NoFreeReservationsInThisPeriodMessage).OverridePropertyName("Reservation");

        When(IntervalUnitIsNotDay, () =>
        {
            RuleFor(WholeContext).Cascade(CascadeMode.Stop)
                .MustAsync(HaveNonNullProductReservation).WithMessage(ReservationDeletedMessage)
                .MustAsync(HaveNonEmptyOrderIdInReservation).WithMessage(AlreadyReservedMessage)
                .OverridePropertyName("Reservation");
        }).Otherwise(() =>
        {
            When(CartItemHasStartAndEndRentalDate, () =>
            {
                RuleFor(CartItemStartRentalDate).LessThanOrEqualTo(CartItemEndRentalDate).When(IncludeBothDates).WithMessage(EndDateMustBeLaterThanStartDateMessage);

                RuleFor(CartItemStartRentalDate).LessThan(CartItemEndRentalDate).When(NotIncludeBothDates).WithMessage(EndDateMustBeLaterThanStartDateMessage);

                RuleFor(CartItemStartRentalDate).GreaterThanOrEqualTo(DateTime.UtcNow).WithMessage(ReservationDatesMustBeLaterThanToday);

                RuleFor(CartItemEndRentalDate).GreaterThanOrEqualTo(DateTime.UtcNow).WithMessage(ReservationDatesMustBeLaterThanToday);

                RuleFor(WholeContext)
                    .MustAsync(HaveNotEmptyCustomerReservations).When(CartItemIsAlreadyInShoppingCart).WithMessage(ReservationNotExistsMessage).OverridePropertyName("Reservation")
                    .DependentRules(() =>
                    {
                        RuleForEach(context => _customerReservations).Cascade(CascadeMode.Stop)
                            .MustAsync(HaveNonNullUnderlyingProductReservation).WithMessage(ReservationDeletedMessage)
                            .Must(HaveEmptyOrderIdInUnderlyingProductReservation).WithMessage(AlreadyReservedMessage);
                    });
            }).Otherwise(() =>
            {
                RuleFor(WholeContext).Must(ReturnMessage).WithMessage(ChooseBothDatesMessage).OverridePropertyName("ReservationDates");
            });
        });
    }

    private static readonly Expression<Func<ShoppingCartReservationProductValidationContext, ShoppingCartReservationProductValidationContext>> WholeContext = context => context;

    private static readonly Expression<Func<ShoppingCartReservationProductValidationContext, string>> ReservationId = context => context.ShoppingCartItem.ReservationId;

    private static readonly Expression<Func<ShoppingCartReservationProductValidationContext, DateTime?>> CartItemStartRentalDate = context => context.ShoppingCartItem.RentalStartDateUtc;

    private static readonly Expression<Func<ShoppingCartReservationProductValidationContext, DateTime?>> CartItemEndRentalDate = context => context.ShoppingCartItem.RentalEndDateUtc;

    private async Task<bool> NotHaveReservationWhichWasAlreadyDoneByCustomer(ShoppingCartReservationProductValidationContext context, CancellationToken cancellation)
    {
        var reservations = await context.ProductReservationService.GetCustomerReservationsHelpers(context.Customer.Id);
        return reservations.All(x => x.ReservationId != context.ShoppingCartItem.ReservationId);
    }

    private static readonly Func<ShoppingCartReservationProductValidationContext, bool> ReservationIdIsProvidedAndItemNotInCart = context =>
        !string.IsNullOrEmpty(context.ShoppingCartItem.ReservationId) && context.Customer.ShoppingCartItems.All(x => x.Id != context.ShoppingCartItem.Id);

    private string AlreadyReservationMessage => _translationService.GetResource("ShoppingCart.AlreadyReservation");

    private static bool IntervalUnitIsNotDay(ShoppingCartReservationProductValidationContext context) => context.Product.IntervalUnitId != IntervalUnit.Day;

    private string NoReservationFoundMessage => _translationService.GetResource("ShoppingCart.Reservation.NoReservationFound");

    private async Task<ProductReservation> GetProductReservation(ShoppingCartReservationProductValidationContext context) =>
        _productReservation ??= await context.ProductReservationService.GetProductReservation(context.ShoppingCartItem.ReservationId);

    private async Task<bool> HaveNonNullProductReservation(ShoppingCartReservationProductValidationContext context, CancellationToken cancellation)
    {
        var reservation = await GetProductReservation(context);
        return reservation != null;
    }

    private string ReservationDeletedMessage => _translationService.GetResource("ShoppingCart.Reservation.ReservationDeleted");

    private string AlreadyReservedMessage => _translationService.GetResource("ShoppingCart.Reservation.AlreadyReserved");

    private async Task<bool> HaveNonEmptyOrderIdInReservation(ShoppingCartReservationProductValidationContext context, CancellationToken cancellation)
    {
        var reservation = await GetProductReservation(context);
        return string.IsNullOrEmpty(reservation.OrderId);
    }

    private static bool CartItemHasStartAndEndRentalDate(ShoppingCartReservationProductValidationContext context) => context.ShoppingCartItem.RentalStartDateUtc.HasValue && context.ShoppingCartItem.RentalEndDateUtc.HasValue;

    private static bool ReturnMessage(ShoppingCartReservationProductValidationContext context) => false;

    private string ChooseBothDatesMessage => _translationService.GetResource("ShoppingCart.Reservation.ChooseBothDates");

    private static bool IncludeBothDates(ShoppingCartReservationProductValidationContext context) => context.Product.IncBothDate;

    private static bool NotIncludeBothDates(ShoppingCartReservationProductValidationContext context) => !context.Product.IncBothDate;

    private string EndDateMustBeLaterThanStartDateMessage => _translationService.GetResource("ShoppingCart.Reservation.EndDateMustBeLaterThanStartDate");

    private string ReservationDatesMustBeLaterThanToday => _translationService.GetResource("ShoppingCart.Reservation.ReservationDatesMustBeLaterThanToday");

    private static bool CartItemIsAlreadyInShoppingCart(ShoppingCartReservationProductValidationContext context) => context.Customer.ShoppingCartItems.Any(x => x.Id == context.ShoppingCartItem.Id);

    private async Task<bool> HaveNotEmptyCustomerReservations(ShoppingCartReservationProductValidationContext context, CancellationToken cancellation)
    {
        _customerReservations = await context.ProductReservationService.GetCustomerReservationsHelperBySciId(context.ShoppingCartItem.Id);
        return _customerReservations.Any();
    }

    private string ReservationNotExistsMessage => _translationService.GetResource("ShoppingCart.Reservation.ReservationNotExists");

    private async Task<bool> HaveNonNullUnderlyingProductReservation(ShoppingCartReservationProductValidationContext context, CustomerReservationsHelper customerReservation, CancellationToken cancellation)
    {
        var productReservation = await context.ProductReservationService.GetProductReservation(customerReservation.ReservationId);
        _customerReservationToProductReservation[customerReservation] = productReservation;
        return productReservation != null;
    }

    private bool HaveEmptyOrderIdInUnderlyingProductReservation(ShoppingCartReservationProductValidationContext context, CustomerReservationsHelper customerReservation)
    {
        var productReservation = _customerReservationToProductReservation[customerReservation];
        return string.IsNullOrEmpty(productReservation.OrderId);
    }

    private static async Task<bool> HaveValidRequestedReservationPeriod(ShoppingCartReservationProductValidationContext context, CancellationToken _)
    {
        var allReservations = await context.ProductReservationService.GetProductReservationsByProductId(context.Product.Id, true, null);
        var helpers = await context.ProductReservationService.GetCustomerReservationsHelpers(context.Customer.Id);
        var otherIds = helpers.Where(h => h.ShoppingCartItemId != context.ShoppingCartItem.Id).Select(h => h.ReservationId).ToHashSet();
        var relevant = allReservations.Where(r => !otherIds.Contains(r.Id));

        var start = context.ShoppingCartItem.RentalStartDateUtc!.Value.Date;
        var end = context.ShoppingCartItem.RentalEndDateUtc!.Value.Date;
        var includeEnd = context.Product.IncBothDate && context.Product.IntervalUnitId == IntervalUnit.Day;
        var requiredDates = Enumerable.Range(0, (end - start).Days + (includeEnd ? 1 : 0)).Select(offset => start.AddDays(offset)).ToList();

        return relevant.GroupBy(r => r.Resource).Any(group =>
        {
            var dates = group.Select(r => r.Date.Date).ToHashSet();
            return requiredDates.All(d => dates.Contains(d));
        });
    }

    private string NoFreeReservationsInThisPeriodMessage => _translationService.GetResource("ShoppingCart.Reservation.NoFreeReservationsInThisPeriod");
}