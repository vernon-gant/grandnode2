using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using Grand.Domain.Orders;
using Grand.SharedKernel.Extensions;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating warnings for shopping cart items, the system must ensure that:
/// 1. Each required attribute mapping in the list of required attribute mappings must have a selected attribute mapping.
/// 2. Each readonly attribute mapping must have matching selected and allowed attribute values parsed from the product using the shopping cart item attributes.
/// 3. Each attribute mapping with allowed validation rules, minimum length set, and control type as TextBox or MultilineTextbox must have the entered text length greater than or equal to the minimum length.
/// 4. Each attribute mapping with allowed validation rules, maximum length set, and control type as TextBox or MultilineTextbox must have the entered text length less than or equal to the maximum length.
public record ShoppingCartItemWarningsValidationContext(Product Product, List<ProductAttributeMappingContext> SelectedProductAttributeMappings, List<ProductAttributeMappingContext> RequiredProductAttributeMappings, ShoppingCartItem ShoppingCartItem);

public record ProductAttributeMappingContext(ProductAttributeMapping Mapping, ProductAttribute ProductAttribute);

public class ShoppingCartItemWarningsValidator : AbstractValidator<ShoppingCartItemWarningsValidationContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartItemWarningsValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleForEach(RequiredProductAttributeMappings)
            .Must(HaveRequiredAttributeSelected).WithMessage(SelectAttributeMessage)
            .Must(HaveReadOnlyAttributeSelected).WithMessage("You cannot change read-only values")
            .Must(HaveEnteredTextLengthGreaterThanOrEqualToMinLengthWhenMappingHasValidationMinLengthAndAttributeControlTypeIsText).WithMessage(TextBoxMinimumLengthMessage)
            .Must(HaveEnteredTextLengthLessThanOrEqualToMaxLengthWhenMappingHasValidationMaxLengthAndAttributeControlTypeIsText).WithMessage(TextBoxMaximumLengthMessage);
    }

    private static readonly Expression<Func<ShoppingCartItemWarningsValidationContext, IEnumerable<ProductAttributeMappingContext>>> RequiredProductAttributeMappings = context => context.RequiredProductAttributeMappings;

    private static bool HaveRequiredAttributeSelected(ShoppingCartItemWarningsValidationContext context, ProductAttributeMappingContext requiredMapping)
    {
        if (!requiredMapping.Mapping.IsRequired) return true;

        return context.SelectedProductAttributeMappings
            .Where(selected => selected.Mapping.Id == requiredMapping.Mapping.Id)
            .SelectMany(selected => ProductExtensions.ParseValues(context.ShoppingCartItem.Attributes, selected.Mapping.Id))
            .Any(value => !string.IsNullOrWhiteSpace(value));
    }

    private string SelectAttributeMessage(ShoppingCartItemWarningsValidationContext context, ProductAttributeMappingContext requiredMapping)
    {
        return !string.IsNullOrEmpty(requiredMapping.Mapping.TextPrompt)
            ? requiredMapping.Mapping.TextPrompt
            : string.Format(_translationService.GetResource("ShoppingCart.SelectAttribute"), requiredMapping.ProductAttribute.Name);
    }

    private static bool HaveReadOnlyAttributeSelected(ShoppingCartItemWarningsValidationContext context, ProductAttributeMappingContext requiredMapping)
    {
        if (requiredMapping.Mapping.AttributeControlTypeId != AttributeControlType.ReadonlyCheckboxes) return true;

        var allowedReadOnlyValueIds = requiredMapping.Mapping.ProductAttributeValues.Where(x => x.IsPreSelected).Select(x => x.Id).ToArray();
        var selectedReadOnlyValueIds = context.Product.ParseProductAttributeValues(context.ShoppingCartItem.Attributes).Select(x => x.Id).ToArray();
        return CommonHelper.ArraysEqual(allowedReadOnlyValueIds, selectedReadOnlyValueIds);
    }

    private static bool HaveEnteredTextLengthGreaterThanOrEqualToMinLengthWhenMappingHasValidationMinLengthAndAttributeControlTypeIsText(ShoppingCartItemWarningsValidationContext context, ProductAttributeMappingContext requiredMapping)
    {
        if (!requiredMapping.Mapping.ValidationRulesAllowed() || !requiredMapping.Mapping.ValidationMinLength.HasValue) return true;
        if (requiredMapping.Mapping.AttributeControlTypeId != AttributeControlType.TextBox && requiredMapping.Mapping.AttributeControlTypeId != AttributeControlType.MultilineTextbox) return true;

        var valuesStr = ProductExtensions.ParseValues(context.ShoppingCartItem.Attributes, requiredMapping.Mapping.Id);
        var enteredText = valuesStr.FirstOrDefault();
        var enteredTextLength = string.IsNullOrEmpty(enteredText) ? 0 : enteredText.Length;

        return enteredTextLength >= requiredMapping.Mapping.ValidationMinLength.Value;
    }

    private string TextBoxMinimumLengthMessage(ShoppingCartItemWarningsValidationContext context, ProductAttributeMappingContext requiredMapping) =>
        string.Format(_translationService.GetResource("ShoppingCart.TextboxMinimumLength"), requiredMapping.ProductAttribute.Name, requiredMapping.Mapping.ValidationMinLength!.Value);

    private static bool HaveEnteredTextLengthLessThanOrEqualToMaxLengthWhenMappingHasValidationMaxLengthAndAttributeControlTypeIsText(ShoppingCartItemWarningsValidationContext context, ProductAttributeMappingContext requiredMapping)
    {
        if (!requiredMapping.Mapping.ValidationRulesAllowed() || !requiredMapping.Mapping.ValidationMaxLength.HasValue) return true;
        if (requiredMapping.Mapping.AttributeControlTypeId != AttributeControlType.TextBox && requiredMapping.Mapping.AttributeControlTypeId != AttributeControlType.MultilineTextbox) return true;

        var valuesStr = ProductExtensions.ParseValues(context.ShoppingCartItem.Attributes, requiredMapping.Mapping.Id);
        var enteredText = valuesStr.FirstOrDefault();
        var enteredTextLength = string.IsNullOrEmpty(enteredText) ? 0 : enteredText.Length;

        return enteredTextLength <= requiredMapping.Mapping.ValidationMaxLength.Value;
    }

    private string TextBoxMaximumLengthMessage(ShoppingCartItemWarningsValidationContext context, ProductAttributeMappingContext requiredMapping) =>
        string.Format(_translationService.GetResource("ShoppingCart.TextboxMaximumLength"), requiredMapping.ProductAttribute.Name, requiredMapping.Mapping.ValidationMaxLength!.Value);
}