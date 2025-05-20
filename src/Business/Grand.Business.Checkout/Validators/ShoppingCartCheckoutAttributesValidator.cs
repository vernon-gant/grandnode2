using FluentValidation;
using Grand.Business.Core.Interfaces.Checkout.CheckoutAttributes;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using Grand.Domain.Common;
using Grand.Domain.Orders;
using System.Linq.Expressions;

/// In the context of validating checkout attributes during shopping cart checkout, the system must ensure that:
/// 1. Every required checkout attribute or attribute with a satisfied condition in the list of all checkout attributes must have at least one non-empty raw checkout attribute when checkout attribute is required and its condition is met which using attribute parser.
/// 2. Every checkout attribute instance in the list of all checkout attribute instances must have entered text length greater than or equal to the minimum length when the attribute is a text-based attribute (TextBox or MultilineTextbox) and validation min length is set.
/// 3. Every checkout attribute instance in the list of all checkout attribute instances must have entered text length less than or equal to the maximum length when the attribute is a text-based attribute (TextBox or MultilineTextbox) and validation max length is set.
public record ShoppingCartCheckoutAttributesContext(
    IReadOnlyList<CustomAttribute> RawCartCheckoutAttributes,
    IReadOnlyList<CheckoutAttribute> AllCheckoutAttributes,
    IReadOnlyList<CheckoutAttribute> ParsedCheckoutAttributes,
    ICheckoutAttributeParser AttributeParser
);

public record CheckoutAttributeWithContext(CheckoutAttribute CheckoutAttribute, ShoppingCartCheckoutAttributesContext Context);

public class ShoppingCartCheckoutAttributesValidator : AbstractValidator<ShoppingCartCheckoutAttributesContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartCheckoutAttributesValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleForEach(AllCheckoutAttributesWithContext).WhereAsync(CheckoutAttributeIsRequiredOrConditionIsMet).Must(HaveAtLeastOneNonEmptyParsedAttribute).WithMessage(MissingAttributeMessage).OverridePropertyName("CheckoutAttribute");

        RuleForEach(AllCheckoutAttributes).Where(IsTextAttributeAndMinimumLengthIsSet).Must(HaveEnteredTextLengthGreaterThanOrEqualToMinimumLength).WithMessage(TextBoxMinimumLengthMessage).OverridePropertyName("CheckoutAttribute");

        RuleForEach(AllCheckoutAttributes).Where(IsTextAttributeAndMaximumLengthIsSet).Must(HaveEnteredTextLengthLessThanOrEqualToMaximumLength).WithMessage(TextBoxMaximumLengthMessage).OverridePropertyName("CheckoutAttribute");
    }

    private static readonly Expression<Func<ShoppingCartCheckoutAttributesContext, IEnumerable<CheckoutAttributeWithContext>>> AllCheckoutAttributesWithContext = ctx => ctx.AllCheckoutAttributes.Select(x => new CheckoutAttributeWithContext(x, ctx));

    private static readonly Expression<Func<ShoppingCartCheckoutAttributesContext, IEnumerable<CheckoutAttribute>>> AllCheckoutAttributes = ctx => ctx.AllCheckoutAttributes;

    private static async Task<bool> CheckoutAttributeIsRequiredOrConditionIsMet(CheckoutAttributeWithContext attributeWithContext)
    {
        var conditionMet = await attributeWithContext.Context.AttributeParser.IsConditionMet(attributeWithContext.CheckoutAttribute, attributeWithContext.Context.RawCartCheckoutAttributes.ToList());
        return attributeWithContext.CheckoutAttribute.IsRequired || conditionMet.HasValue && conditionMet.Value;
    }

    private static bool HaveAtLeastOneNonEmptyParsedAttribute(CheckoutAttributeWithContext attributeWithContext)
    {
        var matchingSelectedAttribute = attributeWithContext.Context.ParsedCheckoutAttributes.FirstOrDefault(x => x.Id == attributeWithContext.CheckoutAttribute.Id);
        var attributeValuesAsStr = attributeWithContext.Context.RawCartCheckoutAttributes.Where(raw => raw.Key == matchingSelectedAttribute?.Id).Select(raw => raw.Value).ToList();
        return attributeValuesAsStr.Any(attributeValuesStr => !string.IsNullOrEmpty(attributeValuesStr.Trim()));
    }

    private string MissingAttributeMessage(ShoppingCartCheckoutAttributesContext context, CheckoutAttributeWithContext attribute) =>
        string.Format(attribute.CheckoutAttribute.TextPrompt ?? _translationService.GetResource("ShoppingCart.SelectAttribute"), attribute.CheckoutAttribute.Name);

    private static bool IsTextAttribute(CheckoutAttribute attribute) => attribute.AttributeControlTypeId is AttributeControlType.TextBox or AttributeControlType.MultilineTextbox;

    private static bool IsTextAttributeAndMinimumLengthIsSet(CheckoutAttribute attribute) => IsTextAttribute(attribute) && attribute.ValidationMinLength.HasValue;

    private static bool IsTextAttributeAndMaximumLengthIsSet(CheckoutAttribute attribute) => IsTextAttribute(attribute) && attribute.ValidationMaxLength.HasValue;

    private static bool HaveEnteredTextLengthGreaterThanOrEqualToMinimumLength(ShoppingCartCheckoutAttributesContext context, CheckoutAttribute attribute)
    {
        var enteredTextLength = context.RawCartCheckoutAttributes.Where(raw => raw.Key == attribute.Id).Select(raw => raw.Value).FirstOrDefault()?.Length ?? 0;
        return enteredTextLength >= attribute.ValidationMinLength!.Value;
    }

    private static bool HaveEnteredTextLengthLessThanOrEqualToMaximumLength(ShoppingCartCheckoutAttributesContext context, CheckoutAttribute attribute)
    {
        var enteredTextLength = context.RawCartCheckoutAttributes.Where(raw => raw.Key == attribute.Id).Select(raw => raw.Value).FirstOrDefault()?.Length ?? 0;
        return enteredTextLength <= attribute.ValidationMaxLength!.Value;
    }

    private string TextBoxMinimumLengthMessage(ShoppingCartCheckoutAttributesContext context, CheckoutAttribute attribute) => string.Format(_translationService.GetResource("ShoppingCart.TextBoxMinimumLength"), attribute.Name, attribute.ValidationMinLength);

    private string TextBoxMaximumLengthMessage(ShoppingCartCheckoutAttributesContext context, CheckoutAttribute attribute) => string.Format(_translationService.GetResource("ShoppingCart.TextBoxMaximumLength"), attribute.Name, attribute.ValidationMaxLength);
}