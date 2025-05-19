#nullable enable

using Grand.Business.Catalog.Services.Validators;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Domain.Catalog;
using Grand.Domain.Common;

namespace Grand.Business.Catalog.Services.Products;

public class StockQuantityService : IStockQuantityService
{
    public virtual int GetTotalStockQuantity(Product product, bool useReservedQuantity = true,
        string warehouseId = "", bool total = false)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (product.ManageInventoryMethodId != ManageInventoryMethod.ManageStock) return 0;

        if (product.UseMultipleWarehouses)
        {
            if (total)
                return useReservedQuantity
                    ? product.ProductWarehouseInventory.Sum(x => x.StockQuantity - x.ReservedQuantity)
                    : product.ProductWarehouseInventory.Sum(x => x.StockQuantity);

            var pwi = product.ProductWarehouseInventory.FirstOrDefault(x => x.WarehouseId == warehouseId);
            if (pwi == null) return 0;
            var result = pwi.StockQuantity;
            if (useReservedQuantity) result -= pwi.ReservedQuantity;

            return result;
        }

        if (string.IsNullOrEmpty(warehouseId) || string.IsNullOrEmpty(product.WarehouseId))
            return product.StockQuantity - (useReservedQuantity ? product.ReservedQuantity : 0);

        if (product.WarehouseId == warehouseId)
            return product.StockQuantity - (useReservedQuantity ? product.ReservedQuantity : 0);

        return 0;
    }

    public virtual int GetTotalStockQuantityForCombination(Product product, ProductAttributeCombination combination,
        bool useReservedQuantity = true, string warehouseId = "")
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(combination);

        if (product.ManageInventoryMethodId != ManageInventoryMethod.ManageStockByAttributes) return 0;

        if (product.UseMultipleWarehouses)
        {
            var pwi = combination.WarehouseInventory.FirstOrDefault(x => x.WarehouseId == warehouseId);
            if (pwi == null) return 0;
            var result = pwi.StockQuantity;
            if (useReservedQuantity) result -= pwi.ReservedQuantity;

            return result;
        }

        if (string.IsNullOrEmpty(warehouseId) || string.IsNullOrEmpty(product.WarehouseId))
            return combination.StockQuantity - (useReservedQuantity ? combination.ReservedQuantity : 0);

        if (product.WarehouseId == warehouseId)
            return combination.StockQuantity - (useReservedQuantity ? combination.ReservedQuantity : 0);

        return 0;
    }

    public virtual (string resource, object? arg0) FormatStockMessage(Product product, string warehouseId, IList<CustomAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(product);

        var basicProductStockMessageFormatter = new BasicProductStockMessageFormatter();
        var conditionalProductStockMessageFormatter = new ConditionalProductStockMessageFormatter(basicProductStockMessageFormatter);
        var productAttributeCombination = product.FindProductAttributeCombination(attributes);
        var validationContext = new ConditionalProductStockMessageFormattingContext(
            product,
            product.ManageInventoryMethodId,
            GetTotalStockQuantityForCombination,
            GetTotalStockQuantity,
            warehouseId,
            productAttributeCombination);
        var validationResult = conditionalProductStockMessageFormatter.Validate(validationContext);
        var toReturn = validationResult.Errors.FirstOrDefault();

        return (toReturn?.ErrorMessage ?? string.Empty, toReturn?.CustomState);
    }
}