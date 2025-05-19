using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Domain.Catalog;
using Grand.Domain.Customers;
using System.Linq.Expressions;

namespace Grand.Business.Catalog.Services.Validators;

/// In the context of evaluating a cross-sell candidate product, the system must ensure that:
/// 1. The candidate cross sell product ID must not already exist in the cart product ids
/// 2. The candidate cross sell product ID must not already exist in the current cross-sell product ids list when rule 1 holds true.
/// 3. The candidate cross sell product must be published when rule 2 holds true.
/// 4. The candidate cross sell product must be authorized by acl service for the current customer when rule 3 holds true.
/// 5. The candidate cross sell product must be authorized by acl service for the current store when rule 4 holds true.
/// 6. The candidate cross sell product must be available when rule 5 holds true.
public record CrossSellCandidateContext(
    string CandidateProductId,
    IReadOnlyList<string> CurrentCrossSellProductIds,
    IReadOnlyList<string> CartProductIds,
    Customer CurrentCustomer,
    string CurrentStoreId,
    Func<string, bool, Task<Product>> GetProductById,
    IAclService AclService);

public class CrossSellCandidateValidator : AbstractValidator<CrossSellCandidateContext>
{
    private Product _crossSellProduct;
    public CrossSellCandidateValidator()
    {
        RuleFor(CandidateProductId).Cascade(CascadeMode.Stop)
            .Must(NotExistInCartProductIds).Must(NotExistInCurrentCrossSellProducts)
            .DependentRules(() =>
            {
                RuleFor(WholeContext).Cascade(CascadeMode.Stop)
                    .MustAsync(HavePublishedCandidateProduct).MustAsync(HaveAuthorizedForCurrentCustomerCandidateProduct)
                    .MustAsync(HaveAuthorizedForCurrentStoreCandidateProduct).MustAsync(HaveAvailableCandidateProduct);
            });
    }

    private static readonly Expression<Func<CrossSellCandidateContext, CrossSellCandidateContext>> WholeContext = context => context;

    private static readonly Expression<Func<CrossSellCandidateContext, string>> CandidateProductId = context => context.CandidateProductId;

    private static bool NotExistInCartProductIds(CrossSellCandidateContext context, string candidateProductId) => !context.CartProductIds.Contains(candidateProductId);

    private bool NotExistInCurrentCrossSellProducts(CrossSellCandidateContext context, string candidateProductId) => !context.CurrentCrossSellProductIds.Contains(candidateProductId);

    public async Task<Product> GetCandidateProductById(CrossSellCandidateContext context) => _crossSellProduct ??= await context.GetProductById(context.CandidateProductId, false);

    private async Task<bool> HavePublishedCandidateProduct(CrossSellCandidateContext context, CancellationToken cancellationToken) => await GetCandidateProductById(context) is { Published: true };

    private async Task<bool> HaveAuthorizedForCurrentCustomerCandidateProduct(CrossSellCandidateContext context, CancellationToken _)
    {
        var candidateProduct = await GetCandidateProductById(context);
        return context.AclService.Authorize(candidateProduct, context.CurrentCustomer);
    }

    private async Task<bool> HaveAuthorizedForCurrentStoreCandidateProduct(CrossSellCandidateContext context, CancellationToken _)
    {
        var candidateProduct = await GetCandidateProductById(context);
        return context.AclService.Authorize(candidateProduct, context.CurrentStoreId);
    }

    private async Task<bool> HaveAvailableCandidateProduct(CrossSellCandidateContext context, CancellationToken _)
    {
        var candidateProduct = await GetCandidateProductById(context);
        return candidateProduct.IsAvailable();
    }
}