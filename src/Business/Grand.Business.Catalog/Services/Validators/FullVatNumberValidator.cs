using FluentValidation;
using Grand.Domain.Tax;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Grand.Business.Catalog.Services.Validators;

/// In the context of validating a full VAT number, the system must ensure that:
/// 1. The validator must return the empty status when the full VAT number is null or empty.
/// 2. The validator must return the invalid status when the full VAT number does not match the regex pattern and rule 1 is not met.
/// 3. The validator must return the matched is code and ise code from the full VAT number using reg ex in the custom state when rule 2 is not met.
public record FullVatNumberValidationContext(string FullVatNumber);

public class FullVatNumberValidator : AbstractValidator<FullVatNumberValidationContext>
{
    public FullVatNumberValidator()
    {
        RuleFor(FullVatNumber)
            .NotEmpty().WithState(EmptyStatus)
            .Matches(ValidVatRegEx).WithState(InvalidStatus)
            .DependentRules(() =>
            {
                RuleFor(FullVatNumber).Must(ReturnVatNumber).WithState(IsoCodeAndVatNumber);
            });
    }

    private static readonly Expression<Func<FullVatNumberValidationContext, string>> FullVatNumber = context => context.FullVatNumber;

    private static object EmptyStatus(FullVatNumberValidationContext context) => VatNumberStatus.Empty;

    private static string ValidVatRegEx => @"^(\w{2})(.*)";

    private static object InvalidStatus(FullVatNumberValidationContext context) => VatNumberStatus.Invalid;

    private static bool ReturnVatNumber(string VatNumber) => false;

    private static object IsoCodeAndVatNumber(FullVatNumberValidationContext context)
    {
        var match = new Regex(ValidVatRegEx).Match(context.FullVatNumber);
        return (match.Groups[1].Value, match.Groups[2].Value);
    }
}