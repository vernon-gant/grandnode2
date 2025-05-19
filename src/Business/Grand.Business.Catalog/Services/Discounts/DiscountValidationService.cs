using Grand.Business.Catalog.Services.Validators;
using Grand.Business.Core.Interfaces.Catalog.Discounts;
using Grand.Business.Core.Queries.Catalog;
using Grand.Business.Core.Utilities.Catalog;
using Grand.Data;
using Grand.Domain.Customers;
using Grand.Domain.Directory;
using Grand.Domain.Discounts;
using Grand.Domain.Orders;
using Grand.Domain.Stores;
using Grand.Infrastructure.Extensions;
using MediatR;

namespace Grand.Business.Catalog.Services.Discounts;

public class DiscountValidationService : IDiscountValidationService
{
    private readonly IRepository<DiscountCoupon> _discountCouponRepository;
    private readonly IDiscountProviderLoader _discountProviderLoader;
    private readonly IMediator _mediator;

    public DiscountValidationService(
        IDiscountProviderLoader discountProviderLoader,
        IRepository<DiscountCoupon> discountCouponRepository,
        IMediator mediator)
    {
        _discountCouponRepository = discountCouponRepository;
        _mediator = mediator;
        _discountProviderLoader = discountProviderLoader;
    }

    /// <summary>
    ///     Validate discount
    /// </summary>
    /// <param name="discount">Discount</param>
    /// <param name="customer">Customer</param>
    /// <param name="store">Store</param>
    /// <param name="currency">Currency</param>
    /// <returns>Discount validation result</returns>
    public virtual async Task<DiscountValidationResult> ValidateDiscount(Discount discount, Customer customer,
        Store store,
        Currency currency)
    {
        ArgumentNullException.ThrowIfNull(discount);

        string[] couponCodesToValidate = null;
        if (customer != null)
            couponCodesToValidate = customer.ParseAppliedCouponCodes(SystemCustomerFieldNames.DiscountCoupons);

        return await ValidateDiscount(discount, customer, store, currency, couponCodesToValidate);
    }

    /// <summary>
    ///     Validate discount
    /// </summary>
    /// <param name="discount">Discount</param>
    /// <param name="customer">Customer</param>
    /// <param name="store">Store</param>
    /// <param name="currency">Currency</param>
    /// <param name="couponCodeToValidate">Coupon code</param>
    /// <returns>Discount validation result</returns>
    public virtual Task<DiscountValidationResult> ValidateDiscount(Discount discount, Customer customer, Store store,
        Currency currency, string couponCodeToValidate)
    {
        var couponCodes = string.IsNullOrWhiteSpace(couponCodeToValidate)
            ? Array.Empty<string>()
            : [
                couponCodeToValidate
            ];
        return ValidateDiscount(discount, customer, store, currency, couponCodes);
    }

    /// <summary>
    ///     Validate discount
    /// </summary>
    /// <param name="discount">Discount</param>
    /// <param name="customer">Customer</param>
    /// <param name="store">Store</param>
    /// <param name="currency">Currency</param>
    /// <param name="couponCodesToValidate">Coupon codes</param>
    /// <returns>Discount validation result</returns>
    public virtual async Task<DiscountValidationResult> ValidateDiscount(Discount discount, Customer customer, Store store, Currency currency, string[] couponCodesToValidate)
    {
        ArgumentNullException.ThrowIfNull(discount);
        ArgumentNullException.ThrowIfNull(customer);

        var result = new DiscountValidationResult();
        var cart = customer.ShoppingCartItems.Where(sci => sci.ShoppingCartTypeId == ShoppingCartType.ShoppingCart).ToList();
        var basicEligibilityContext = new BasicEligibilityContext(discount, currency.CurrencyCode, store, cart, _mediator, customer, _discountProviderLoader, couponCodesToValidate, _discountCouponRepository);
        var basicEligibilityValidationResult = await new BasicEligibilityValidator().ValidateAsync(basicEligibilityContext);

        if (!basicEligibilityValidationResult.IsValid)
        {
            result.UserErrorResource = basicEligibilityValidationResult.Errors.FirstOrDefault()?.ErrorMessage ?? string.Empty;
            return result;
        }

        result.IsValid = true;
        return result;
    }

    /// <summary>
    ///     Exist coupon code in discount
    /// </summary>
    /// <param name="couponCode"></param>
    /// <param name="discountId"></param>
    /// <param name="used"></param>
    /// <returns></returns>
    public async Task<bool> ExistsCodeInDiscount(string couponCode, string discountId, bool? used)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
            return false;

        var query = _discountCouponRepository.Table.Where(x => x.CouponCode == couponCode
                                                               && x.DiscountId == discountId);

        if (used.HasValue)
            query = query.Where(x => x.Used == used.Value);

        var result = await Task.FromResult(query.ToList());
        return result.Count != 0;
    }
}