using FluentValidation;
using Grand.Domain.Customers;
using System.Linq.Expressions;

namespace Grand.Business.Common.Validators;

// In the context of checking if a customer is in a customer group, the system must ensure that:
// 1. The customer must not be null.
// 2. The customer group system name must not be null or empty and rule 1 holds true.
// 3. The customer group found by the system name must be contained in the customer groups when only active customer groups are allowed and rule 2 holds true.
// 4. The customer group found by the system name must have matching system name with the provided customer group system name when rule 2 holds true.
// 5. The customer group found by the system name must have the same IsSystem value as the provided IsSystem when the provided IsSystem is not null and rule 2 holds true.
public record IsInCustomerGroupContext(Customer Customer, string CustomerGroupSystemName, bool OnlyActiveCustomerGroups, bool? IsSystem, Func<string, Task<CustomerGroup>> GetCustomerGroupBySystemName);

public class IsInCustomerGroupValidator : AbstractValidator<IsInCustomerGroupContext>
{
    private CustomerGroup _customerGroupBySystemName;

    public IsInCustomerGroupValidator()
    {
        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .Must(HaveNonNullCustomer).Must(HaveNonNullCustomerGroupSystemName).MustAsync(RetrieveCustomerGroupBySystemNameBeforeValidation)
            .DependentRules(() =>
            {
                RuleFor(_ => _customerGroupBySystemName).Cascade(CascadeMode.Stop)
                    .Must(BeContainedInCustomerGroup)
                    .Must(BeActiveCustomerGroup).When(OnlyActiveCustomerGroupsAreAllowed)
                    .Must(HaveMatchingSystemNameWithProvidedCustomerGroupSystemName)
                    .Must(HaveSameIsSystemValueAsProvided).When(ProvidedIsSystemIsNotNull);
            });
    }

    private static readonly Expression<Func<IsInCustomerGroupContext, IsInCustomerGroupContext>> WholeContext = context => context;

    private static bool HaveNonNullCustomer(IsInCustomerGroupContext context) => context.Customer != null;

    private static bool HaveNonNullCustomerGroupSystemName(IsInCustomerGroupContext context) => !string.IsNullOrEmpty(context.CustomerGroupSystemName);

    private async Task<bool> RetrieveCustomerGroupBySystemNameBeforeValidation(IsInCustomerGroupContext context, CancellationToken cancellationToken)
    {
        _customerGroupBySystemName = await context.GetCustomerGroupBySystemName(context.CustomerGroupSystemName);
        return true;
    }

    private bool BeContainedInCustomerGroup(IsInCustomerGroupContext context, CustomerGroup customerGroup) => context.Customer.Groups.Contains(customerGroup.Id);


    private bool BeActiveCustomerGroup(IsInCustomerGroupContext context, CustomerGroup customerGroup) => customerGroup.Active;

    private bool OnlyActiveCustomerGroupsAreAllowed(IsInCustomerGroupContext context) => context.OnlyActiveCustomerGroups;

    private bool HaveMatchingSystemNameWithProvidedCustomerGroupSystemName(IsInCustomerGroupContext context, CustomerGroup customerGroup) => customerGroup.SystemName == context.CustomerGroupSystemName;

    private bool HaveSameIsSystemValueAsProvided(IsInCustomerGroupContext context, CustomerGroup customerGroup) => customerGroup.IsSystem == context.IsSystem;

    private bool ProvidedIsSystemIsNotNull(IsInCustomerGroupContext context) => context.IsSystem.HasValue;
}