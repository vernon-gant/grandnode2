using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Domain.Common;
using Grand.Domain.Customers;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Grand.Business.Authentication.Validators;

/// In the context of retrieving a customer, the system must ensure that:
/// 1.  The customer must not be null.
/// 2.  The passwordToken must be equal to the UserData claim in the principal when PasswordToken is not empty and rule 2 holds true.
/// 3.  The customer must be active when rule 1 holds true.
/// 4.  The customer must not be deleted when rule 1 holds true.
/// 5.  The customer must be registered in at least one valid group with the check performed by the group service when rule 1 holds true.
public record AuthenticatedCustomerValidationContext(Customer Customer, ClaimsPrincipal Principal, IGroupService GroupService);

public class AuthenticatedCustomerValidator : AbstractValidator<AuthenticatedCustomerValidationContext>
{
    public AuthenticatedCustomerValidator()
    {
        RuleFor(Customer)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(PasswordToken).Must(BeEqualToUserDataClaim).When(TokenNotEmpty).OverridePropertyName(nameof(SystemCustomerFieldNames.PasswordToken));

                RuleFor(Customer).Must(BeActive).Must(BeNotDeleted).MustAsync(BeRegisteredInAtLeastOneValidGroup);
            });
    }

    private readonly Expression<Func<AuthenticatedCustomerValidationContext, Customer>> Customer = context => context.Customer;

    private readonly Expression<Func<AuthenticatedCustomerValidationContext, string>> PasswordToken = context => context.Customer.GetUserFieldFromEntity<string>(SystemCustomerFieldNames.PasswordToken, "");

    private static bool BeEqualToUserDataClaim(AuthenticatedCustomerValidationContext context, string storedToken)
    {
        var claim = context.Principal.FindFirst(c => c.Type == ClaimTypes.UserData && c.Issuer.Equals(context.Principal.Identity?.AuthenticationType, StringComparison.InvariantCultureIgnoreCase));

        return claim is not null && claim.Value == storedToken;
    }

    private static bool TokenNotEmpty(AuthenticatedCustomerValidationContext context) => !string.IsNullOrEmpty(context.Customer.GetUserFieldFromEntity<string>(SystemCustomerFieldNames.PasswordToken, ""));

    private static bool BeActive(Customer customer) => customer.Active;

    private static bool BeNotDeleted(Customer customer) => !customer.Deleted;

    private static Task<bool> BeRegisteredInAtLeastOneValidGroup(AuthenticatedCustomerValidationContext context, Customer _, CancellationToken token) => context.GroupService.IsRegistered(context.Customer);
}