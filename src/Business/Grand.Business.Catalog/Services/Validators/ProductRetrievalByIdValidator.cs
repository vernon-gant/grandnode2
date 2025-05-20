using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Domain.Catalog;
using Grand.Domain.Customers;
using System.Linq.Expressions;

namespace Grand.Business.Catalog.Services.Validators;

/// In the context of retrieving products by identifiers, the system must ensure that:
/// 1. The product product must not be null.
/// 2. The product product must be authorized by acl service for the current customer when hidden products are not shown and rule 1 holds true.
/// 3. The product product must be authorized by acl service for the current store when hidden products are not shown and rule 2 holds true.
/// 4. The product product must be available when hidden products are not shown and rule 3 holds true.
public record ProductRetrievalByIdContext(Product RetrievedProduct, bool ShowHidden, Customer CurrentCustomer, string CurrentStoreId, IAclService AclService);

public class ProductRetrievalByIdValidator : AbstractValidator<ProductRetrievalByIdContext>
{
    public ProductRetrievalByIdValidator()
    {
        RuleFor(RetrievedProduct).Cascade(CascadeMode.Stop).NotNull().Must(BeAuthorizedForCurrentCustomer).Must(BeAuthorizedForCurrentStore).Must(BeAvailable).When(ProductIsNotNullAndHiddenProductsAreNotShown);
    }

    private static readonly Expression<Func<ProductRetrievalByIdContext, Product>> RetrievedProduct = context => context.RetrievedProduct;

    private static bool ProductIsNotNullAndHiddenProductsAreNotShown(ProductRetrievalByIdContext context) => !context.ShowHidden && context.RetrievedProduct != null;

    private static bool BeAuthorizedForCurrentCustomer(ProductRetrievalByIdContext context, Product retrievedProduct) => context.AclService.Authorize(retrievedProduct, context.CurrentCustomer);

    private static bool BeAuthorizedForCurrentStore(ProductRetrievalByIdContext context, Product retrievedProduct) => context.AclService.Authorize(retrievedProduct, context.CurrentStoreId);

    private static bool BeAvailable(ProductRetrievalByIdContext context, Product retrievedProduct) => retrievedProduct.IsAvailable();
}