using FluentValidation;
using Grand.Business.Core.Extensions;
using Grand.Business.Core.Interfaces.Authentication;
using Grand.Domain.Customers;
using Grand.Domain.Stores;
using Grand.Infrastructure.Extensions;
using System.Linq.Expressions;

namespace Grand.Business.Authentication.Validators;

/// In the context of checking the availability of an external authentication provider, the system must ensure that:
/// 1.  The provider must not be null.
/// 2.  The provider must be active.
/// 3.  The provider must allow authentication for the current customer group using the external authentication settings.
/// 4.  The provider must allow authentication for the current store.
public record AuthenticationProviderAvailabilityContext(IExternalAuthenticationProvider Provider, Customer CurrentCustomer, Store CurrentStore, ExternalAuthenticationSettings Settings);

public class AuthenticationProviderAvailabilityValidator : AbstractValidator<AuthenticationProviderAvailabilityContext>
{
    public AuthenticationProviderAvailabilityValidator()
    {
        RuleFor(Provider).Cascade(CascadeMode.Stop)
            .NotNull().Must(BeActive).Must(AllowAuthenticationForCustomerGroup).Must(AllowAuthenticationForCurrentStore);
    }

    private static readonly Expression<Func<AuthenticationProviderAvailabilityContext, IExternalAuthenticationProvider>> Provider = context => context.Provider;

    private bool BeActive(AuthenticationProviderAvailabilityContext context, IExternalAuthenticationProvider provider) => provider.IsMethodActive(context.Settings);

    private static bool AllowAuthenticationForCustomerGroup(AuthenticationProviderAvailabilityContext context, IExternalAuthenticationProvider provider) => provider.IsAuthenticateGroup(context.CurrentCustomer);

    private static bool AllowAuthenticationForCurrentStore(AuthenticationProviderAvailabilityContext context, IExternalAuthenticationProvider provider) => provider.IsAuthenticateStore(context.CurrentStore);
}