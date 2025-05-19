using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Domain.Catalog;
using Grand.Domain.Customers;
using System.Linq.Expressions;

namespace Grand.Business.Catalog.Services.Validators;

/// In the context of building a category breadcrumb, the system must ensure that:
/// 1.  The category must not be null.
/// 2.  The category must be published must be true when showHidden is false and rule 1 holds true.
/// 3.  The category must not be in the list of already processed category ids when rule 2 holds true.
/// 4.  The ACL service must authorize the Category for the current customer when showHidden is false and rule 2 holds true.
/// 5.  The ACL service must authorize the Category for the current store id when showHidden is false and rule 3 holds true.
public record CategoryBreadcrumbBuildingContext(Category Category, bool ShowHidden, Customer CurrentCustomer, string CurrentStoreId, IReadOnlyList<string> AlreadyProcessedCategoryIds, IAclService AclService);

public class CategoryBreadcrumbBuildingValidator : AbstractValidator<CategoryBreadcrumbBuildingContext>
{
    public CategoryBreadcrumbBuildingValidator()
    {
        RuleFor(Category).Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(BePublished).When(ShowHiddenIsFalse, ApplyConditionTo.CurrentValidator)
            .Must(NotBeAlreadyProcessed)
            .DependentRules(() =>
            {
                RuleFor(AclService).Must(AuthorizeCategoryForCurrentCustomer).Must(AuthorizeCategoryForCurrentStore).When(ShowHiddenIsFalse);
            });
    }

    private static Expression<Func<CategoryBreadcrumbBuildingContext, Category>> Category = ctx => ctx.Category;

    private static Expression<Func<CategoryBreadcrumbBuildingContext, IAclService>> AclService = ctx => ctx.AclService;

    private static bool BePublished(Category category) => category.Published;

    private static bool ShowHiddenIsFalse(CategoryBreadcrumbBuildingContext context) => !context.ShowHidden;

    private static bool NotBeAlreadyProcessed(CategoryBreadcrumbBuildingContext context, Category category) => !context.AlreadyProcessedCategoryIds.Contains(category.Id);

    private static bool AuthorizeCategoryForCurrentCustomer(CategoryBreadcrumbBuildingContext context, IAclService aclService) => aclService.Authorize(context.Category, context.CurrentCustomer);

    private static bool AuthorizeCategoryForCurrentStore(CategoryBreadcrumbBuildingContext context, IAclService aclService) => aclService.Authorize(context.Category, context.CurrentStoreId);
}