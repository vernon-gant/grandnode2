using FluentValidation;
using Grand.Business.Core.Interfaces.Catalog.Discounts;
using Grand.Business.Core.Queries.Catalog;
using Grand.Business.Core.Utilities.Catalog;
using Grand.Data;
using Grand.Domain;
using Grand.Domain.Customers;
using Grand.Domain.Discounts;
using Grand.Domain.Orders;
using Grand.Domain.Stores;
using Grand.Infrastructure.Extensions;
using MediatR;
using System.Linq.Expressions;

namespace Grand.Business.Catalog.Services.Validators;

/// In the context of basic discount eligibility, the system must ensure that:
/// 1. The discount must be enabled.
/// 2. The currency code must match the discount currency when rule 1 holds true.
/// 3. The discount start date must be less than or equal to now when start date is specified and rule 2 holds true.
/// 4. The discount end date must be greater than or equal to now when end date is specified and rule 2 holds true.
/// 5. The discount store id list must contain the current store id when the discount is limited to stores and rule 2 holds true.
/// 6. The customer shopping cart must not contain any gift vouchers when discount type is AssignedToOrderTotal or AssignedToOrderSubTotal and rule 2 holds true.
/// 7. The discount use times obtained using the mediator and customer must be less that used times retrieved from mediator when discount limitation id is equal to NTimes and rule 2 holds true.
/// 8. The discount use times obtained using the mediator and customer must be less that used times retrieved from mediator using customer id when discount limitation id is equal to NTimesPerUser and rule 2 holds true.
/// 9. Each rule in discount rules obtained using the discount provider loader must have a non null single requirement retrieved from the requirement plugin using provider loader when requirement plugin is not null, authenticates the store and rule 2 holds true.
/// 10. Each rule in discount rules obtained using the discount provider loader must have a single requirement rule which is fulfilled when requirement plugin is not null, authenticates the store and rule 2 holds true.
/// 11. The coupon codes collection must not be empty when discount requires coupon code is true and rule 2 holds true.
/// 12. Each coupon code in the coupon codes collection must exist in the discount’s coupon table when rule 9 holds true.
public record BasicEligibilityContext(Discount Discount, string CurrencyCode, Store Store, IReadOnlyList<ShoppingCartItem> CustomerCart,
    IMediator Mediator, Customer Customer, IDiscountProviderLoader DiscountProviderLoader, string[] CouponCodes, IRepository<DiscountCoupon> DiscountCouponRepository);

public record struct DiscountRuleContext(DiscountRule DiscountRule, IDiscountProvider RequirementPlugin, Store Store);

public record struct CouponCodeWithContext(string Code, BasicEligibilityContext Context);

public class BasicEligibilityValidator : AbstractValidator<BasicEligibilityContext>
{
    public BasicEligibilityValidator()
    {
        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .Must(HaveEnabledDiscount)
            .Must(HaveMatchingCurrencyCode)
            .DependentRules(() =>
            {
                RuleFor(WholeContext)
                    .Must(HaveDiscountStartDateLessThanNow).When(StartDateIsSet, ApplyConditionTo.CurrentValidator)
                    .WithMessage("ShoppingCart.Discount.NotStartedYet")
                    .Must(HaveDiscountEndDateGreaterThanNow).When(EndDateIsSet, ApplyConditionTo.CurrentValidator)
                    .WithMessage("ShoppingCart.Discount.Expired")
                    .Must(HaveStoreIdPresentInDiscountStoreIdList).When(DiscountIsLimitedToStores, ApplyConditionTo.CurrentValidator)
                    .WithMessage("ShoppingCart.Discount.CannotBeUsedInStore")
                    .Must(HaveCustomerShoppingCartNotContainingGiftVouchers).When(DiscountTypeIsAssignedToOrderTotalOrSubTotal, ApplyConditionTo.CurrentValidator)
                    .WithMessage("ShoppingCart.Discount.CannotBeUsedWithGiftVouchers")
                    .MustAsync(HaveDiscountUsedTimesLessThanLimitationTimesNTimes).When(DiscountLimitationIdIsNTimes, ApplyConditionTo.CurrentValidator)
                    .MustAsync(HaveDiscountUsedTimesLessThanLimitationTimesNTimesPerUser).When(DiscountLimitationIdIsNTimesPerUser, ApplyConditionTo.CurrentValidator)
                    .WithMessage("ShoppingCart.Discount.CannotBeUsedAnymore");

                RuleForEach(DiscountRulesWithContext).Cascade(CascadeMode.Stop)
                    .Where(RequirementPluginIsNotNull)
                    .Where(RequirementPluginAuthenticatesStore)
                    .Must(HaveNonNullSingleRequirementRule)
                    .MustAsync(HaveSingleRequirementRuleWhichIsFulfilled)
                    .OverridePropertyName("DiscountRules");

                RuleFor(CouponCodesWithContext)
                    .Must(HaveAtLeastOnePresentCouponCodeInDiscount)
                    .When(DiscountRequiresCouponCode)
                    .OverridePropertyName("CouponCodes");
            });
    }

    private static readonly Expression<Func<BasicEligibilityContext, BasicEligibilityContext>> WholeContext = context => context;

    private static readonly Expression<Func<BasicEligibilityContext, IEnumerable<DiscountRuleContext>>> DiscountRulesWithContext = context => GetDiscountRulesWithContext(context);

    private static readonly Expression<Func<BasicEligibilityContext, IEnumerable<CouponCodeWithContext>>> CouponCodesWithContext = context => GetCouponCodesWithContext(context);

    private static List<DiscountRuleContext> GetDiscountRulesWithContext(BasicEligibilityContext context)
    {
        return context.Discount.DiscountRules
            .Select(rule => new DiscountRuleContext(rule, context.DiscountProviderLoader.LoadDiscountProviderByRuleSystemName(rule.DiscountRequirementRuleSystemName), context.Store))
            .ToList();
    }

    private static List<CouponCodeWithContext> GetCouponCodesWithContext(BasicEligibilityContext context)
    {
        return context.CouponCodes
            .Select(code => new CouponCodeWithContext(code, context))
            .ToList();
    }

    private static bool HaveEnabledDiscount(BasicEligibilityContext context) => context.Discount.IsEnabled;

    private static bool HaveMatchingCurrencyCode(BasicEligibilityContext context) => context.Discount.CurrencyCode == context.CurrencyCode;

    private static bool HaveDiscountStartDateLessThanNow(BasicEligibilityContext context)
    {
        var startDate = DateTime.SpecifyKind(context.Discount.StartDateUtc!.Value, DateTimeKind.Utc);
        return startDate.CompareTo(DateTime.UtcNow) <= 0;
    }

    private static bool StartDateIsSet(BasicEligibilityContext context) => context.Discount.StartDateUtc.HasValue;

    private static bool HaveDiscountEndDateGreaterThanNow(BasicEligibilityContext context)
    {
        var endDate = DateTime.SpecifyKind(context.Discount.EndDateUtc!.Value, DateTimeKind.Utc);
        return endDate.CompareTo(DateTime.UtcNow) >= 0;
    }

    private static bool EndDateIsSet(BasicEligibilityContext context) => context.Discount.EndDateUtc.HasValue;

    private static bool HaveStoreIdPresentInDiscountStoreIdList(BasicEligibilityContext context) => context.Discount.Stores.Contains(context.Store.Id);

    private static bool DiscountIsLimitedToStores(BasicEligibilityContext context) => context.Discount.LimitedToStores;

    private static bool HaveCustomerShoppingCartNotContainingGiftVouchers(BasicEligibilityContext context) => context.CustomerCart.Any(x => x.IsGiftVoucher);

    private static bool DiscountTypeIsAssignedToOrderTotalOrSubTotal(BasicEligibilityContext context) => context.Discount.DiscountTypeId is DiscountType.AssignedToOrderTotal or DiscountType.AssignedToOrderSubTotal;

    private static bool DiscountUsedTimesLessThanLimitation(IPagedList<DiscountUsageHistory> usedTimes, Discount discount) => usedTimes.TotalCount < discount.LimitationTimes;

    private static async Task<bool> HaveDiscountUsedTimesLessThanLimitationTimesNTimes(BasicEligibilityContext context, CancellationToken _)
    {
        var usedTimes = await context.Mediator.Send(new GetDiscountUsageHistoryQuery { DiscountId = context.Discount.Id, PageSize = 1 });
        return DiscountUsedTimesLessThanLimitation(usedTimes, context.Discount);
    }

    private static bool DiscountLimitationIdIsNTimes(BasicEligibilityContext context) => context.Discount.DiscountLimitationId == DiscountLimitationType.NTimes;

    private static async Task<bool> HaveDiscountUsedTimesLessThanLimitationTimesNTimesPerUser(BasicEligibilityContext context, CancellationToken _)
    {
        var usedTimes = await context.Mediator.Send(new GetDiscountUsageHistoryQuery { DiscountId = context.Discount.Id, CustomerId = context.Customer.Id, PageSize = 1 });
        return DiscountUsedTimesLessThanLimitation(usedTimes, context.Discount);
    }

    private static bool DiscountLimitationIdIsNTimesPerUser(BasicEligibilityContext context) => context.Discount.DiscountLimitationId == DiscountLimitationType.NTimesPerUser;

    private static IDiscountRule SingleRequirementRule(DiscountRuleContext rule)
    {
        return rule.RequirementPlugin.GetRequirementRules().FirstOrDefault(x => x.SystemName.Equals(rule.DiscountRule.DiscountRequirementRuleSystemName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HaveNonNullSingleRequirementRule(BasicEligibilityContext context, DiscountRuleContext rule)
    {
        var singleRequirementRule = SingleRequirementRule(rule);
        return singleRequirementRule != null;
    }

    private static async Task<bool> HaveSingleRequirementRuleWhichIsFulfilled(BasicEligibilityContext context, DiscountRuleContext rule, CancellationToken _)
    {
        var singleRequirementRule = SingleRequirementRule(rule);

        var ruleRequest = new DiscountRuleValidationRequest {
            DiscountRule = rule.DiscountRule,
            Discount = context.Discount,
            Customer = context.Customer,
            Store = context.Store
        };

        var ruleResult = await singleRequirementRule.CheckRequirement(ruleRequest);
        return ruleResult.IsValid;
    }

    private static bool RequirementPluginIsNotNull(DiscountRuleContext rule) => rule.RequirementPlugin != null;

    private static bool RequirementPluginAuthenticatesStore(DiscountRuleContext rule) => rule.RequirementPlugin.IsAuthenticateStore(rule.Store);

    private static bool DiscountRequiresCouponCode(BasicEligibilityContext context) => context.Discount.RequiresCouponCode;

    private static bool HaveAtLeastOnePresentCouponCodeInDiscount(IEnumerable<CouponCodeWithContext> coupons) => coupons.Any(coupon => ExistsCodeInDiscount(coupon.Code, coupon.Context.DiscountCouponRepository, coupon.Context.Discount.Id, coupon.Context.Discount.Reused));

    private static bool ExistsCodeInDiscount(string couponCode, IRepository<DiscountCoupon> repository, string discountId, bool? used)
    {
        var query = repository.Table.Where(x => x.CouponCode == couponCode && x.DiscountId == discountId);

        if (used.HasValue)
            query = query.Where(x => x.Used == used.Value);

        return query.ToList().Count != 0;
    }
}