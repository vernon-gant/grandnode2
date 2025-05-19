using FluentValidation;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using Grand.Domain.Orders;

namespace Grand.Business.Checkout.Validators;

public record ShoppingCartItemAttributeValidationContext(Product Product, ShoppingCartItem ShoppingCartItem, bool IgnoreNonCombinableAttributes);

public class ShoppingCartItemAttributeValidator : AbstractValidator<ShoppingCartItemAttributeValidationContext>
{
    public ShoppingCartItemAttributeValidator(ITranslationService translationService, IProductService productService, IProductAttributeService productAttributeService, IValidator<ShoppingCartItemWarningsValidationContext> warningsValidator)
    {
        RuleFor(x => x).CustomAsync(async (value, context, _) =>
        {
            var nonNullBundleProducts = await value.Product.GetNonNullRawBundleProductsAsync(productService.GetProductById);
            var selectedAttributeMappingsTasks = value.Product.GetSelectedProductAttributeMappings(value.ShoppingCartItem.Attributes, nonNullBundleProducts, value.IgnoreNonCombinableAttributes)
                .Select(async mapping => new ProductAttributeMappingContext(mapping, await productAttributeService.GetProductAttributeById(mapping.ProductAttributeId))).ToList();
            var selectedAttributeMappings = await Task.WhenAll(selectedAttributeMappingsTasks);
            var requiredAttributeMappingsTasks = value.Product.GetRequiredProductAttributeMappings(value.ShoppingCartItem.Attributes, nonNullBundleProducts, value.IgnoreNonCombinableAttributes)
                .Select(async mapping => new ProductAttributeMappingContext(mapping, await productAttributeService.GetProductAttributeById(mapping.ProductAttributeId))).ToList();
            var requiredAttributeMappings = await Task.WhenAll(requiredAttributeMappingsTasks);
            var warningsValidationContext = new ShoppingCartItemWarningsValidationContext(value.Product, selectedAttributeMappings.ToList(), requiredAttributeMappings.ToList(), value.ShoppingCartItem);
            var warnings = warningsValidator.Validate(warningsValidationContext).Errors.Select(x => x.ErrorMessage).ToList();

            if (warnings.Any())
            {
                warnings.ToList().ForEach(context.AddFailure);
                return;
            }


            //validate bundled products
            var attributeValues = value.Product.ParseProductAttributeValues(value.ShoppingCartItem.Attributes);
            foreach (var attributeValue in attributeValues)
            {
                var productAttributeMapping =
                    value.Product.ProductAttributeMappings.FirstOrDefault(x =>
                        x.ProductAttributeValues.Any(z => z.Id == attributeValue.Id));
                if (attributeValue.AttributeValueTypeId != AttributeValueType.AssociatedToProduct ||
                    productAttributeMapping == null) continue;
                {
                    if (value.IgnoreNonCombinableAttributes && productAttributeMapping.IsNonCombinable())
                        continue;

                    //associated product (bundle)
                    var associatedProduct = await productService.GetProductById(attributeValue.AssociatedProductId);
                    if (associatedProduct != null)
                    {
                        var totalQty = value.ShoppingCartItem.Quantity * attributeValue.Quantity;
                        var associatedProductNonNullBundleProducts = await associatedProduct.GetNonNullRawBundleProductsAsync(productService.GetProductById);
                        var associatedProductAttributeMappingsTasks = associatedProduct.GetSelectedProductAttributeMappings(value.ShoppingCartItem.Attributes, associatedProductNonNullBundleProducts, value.IgnoreNonCombinableAttributes)
                            .Select(async mapping => new ProductAttributeMappingContext(mapping, await productAttributeService.GetProductAttributeById(mapping.ProductAttributeId))).ToList();
                        var associatedProductAttributeMappings = await Task.WhenAll(associatedProductAttributeMappingsTasks);
                        var associatedProductRequiredAttributeMappingsTasks = associatedProduct.GetRequiredProductAttributeMappings(value.ShoppingCartItem.Attributes, associatedProductNonNullBundleProducts, value.IgnoreNonCombinableAttributes)
                            .Select(async mapping => new ProductAttributeMappingContext(mapping, await productAttributeService.GetProductAttributeById(mapping.ProductAttributeId))).ToList();
                        var associatedProductRequiredAttributeMappings = await Task.WhenAll(associatedProductRequiredAttributeMappingsTasks);
                        var associatedProductWarningsValidationContext = new ShoppingCartItemWarningsValidationContext(associatedProduct, associatedProductAttributeMappings.ToList(), associatedProductRequiredAttributeMappings.ToList(), new ShoppingCartItem {
                            ShoppingCartTypeId = value.ShoppingCartItem.ShoppingCartTypeId,
                            StoreId = value.ShoppingCartItem.StoreId,
                            Quantity = totalQty,
                            WarehouseId = value.ShoppingCartItem.WarehouseId,
                            Attributes = value.ShoppingCartItem.Attributes
                        });
                        var associatedProductWarnings = warningsValidator.Validate(associatedProductWarningsValidationContext).Errors.Select(x => x.ErrorMessage).ToList();
                        foreach (var associatedProductWarning in associatedProductWarnings)
                        {
                            var productAttribute = await productAttributeService.GetProductAttributeById(productAttributeMapping.ProductAttributeId);
                            var attributeName = productAttribute.Name;
                            var attributeValueName = attributeValue.Name;
                            warnings.Add(string.Format(translationService.GetResource("ShoppingCart.AssociatedAttributeWarning"), attributeName, attributeValueName, associatedProductWarning));
                        }
                    }
                    else
                    {
                        warnings.Add($"Associated product cannot be loaded - {attributeValue.AssociatedProductId}");
                    }

                    if (!warnings.Any()) continue;
                    warnings.ToList().ForEach(context.AddFailure);
                    return;
                }
            }
        });
    }
}