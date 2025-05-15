using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Customers;
using Grand.Infrastructure.Validators;
using Grand.Module.Api.DTOs.Customers;

namespace Grand.Module.Api.Validators.Customers;

public class PasswordValidator : BaseGrandValidator<PasswordDto>
{
    public PasswordValidator(
        IEnumerable<IValidatorConsumer<PasswordDto>> validators,
        ITranslationService translationService,
        CustomerSettings customerSettings)
        : base(validators)
    {
        RuleFor(x => x.Password).NotEmpty()
            .WithMessage(translationService.GetResource("Account.Fields.Password.Required"));

        if (!string.IsNullOrEmpty(customerSettings.PasswordRegularExpression))
            RuleFor(x => x.Password).Matches(customerSettings.PasswordRegularExpression)
                .WithMessage(string.Format(translationService.GetResource("Account.Fields.Password.Validation")));
    }
}