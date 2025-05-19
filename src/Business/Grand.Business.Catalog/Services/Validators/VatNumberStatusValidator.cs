using FluentValidation;
using Grand.Domain.Tax;
using System.Linq.Expressions;

namespace Grand.Business.Catalog.Services.Validators;

/// In the context of validating a VAT number status, the system must ensure that:
/// 1. The validator must return empty status when the VAT number is null or empty.
/// 2. The validator must return empty status when two-letter ISO code is null or empty and rule 1 is not held
/// 3. The validator must return valid status when the tax settings assume eu vat is valid and rule 2 is not held
/// 4. The validator must return unknown status when the tax settings web service is not used and rule 3 is not held
public record VatNumberStatusValidationContext(string VatNumber, string TwoLetterIsoCode, TaxSettings TaxSettings);

public class VatNumberStatusValidator : AbstractValidator<VatNumberStatusValidationContext>
{
    public VatNumberStatusValidator()
    {
        RuleFor(WholeContext).Cascade(CascadeMode.Stop)
            .Must(ReturnStatus).When(VatNumberIsNull, ApplyConditionTo.CurrentValidator).WithState(EmptyStatus)
            .Must(ReturnStatus).When(TwoLetterIsoCodeIsNull, ApplyConditionTo.CurrentValidator).WithState(EmptyStatus)
            .Must(ReturnStatus).When(TaxSettingsAssumeEuVatIsValid, ApplyConditionTo.CurrentValidator).WithState(ValidStatus)
            .Must(ReturnStatus).When(TaxSettingsWebServiceIsNotUsed, ApplyConditionTo.CurrentValidator).WithState(UnknownStatus);
    }

    private static readonly Expression<Func<VatNumberStatusValidationContext, VatNumberStatusValidationContext>> WholeContext = context => context;

    private static bool ReturnStatus(VatNumberStatusValidationContext context) => false;

    private static bool VatNumberIsNull(VatNumberStatusValidationContext context) => string.IsNullOrEmpty(context.VatNumber);

    private static bool TwoLetterIsoCodeIsNull(VatNumberStatusValidationContext context) => string.IsNullOrEmpty(context.TwoLetterIsoCode);

    private static bool TaxSettingsAssumeEuVatIsValid(VatNumberStatusValidationContext context) => context.TaxSettings.EuVatAssumeValid;

    private static bool TaxSettingsWebServiceIsNotUsed(VatNumberStatusValidationContext context) => !context.TaxSettings.EuVatUseWebService;

    private static object EmptyStatus(VatNumberStatusValidationContext context) => VatNumberStatus.Empty;

    private static object ValidStatus(VatNumberStatusValidationContext context) => VatNumberStatus.Valid;

    private static object UnknownStatus(VatNumberStatusValidationContext context) => VatNumberStatus.Unknown;
}