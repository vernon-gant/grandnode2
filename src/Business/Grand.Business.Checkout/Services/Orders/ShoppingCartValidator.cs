using Grand.Business.Checkout.Validators;
using Grand.Business.Core.Extensions;
using Grand.Business.Core.Interfaces.Catalog.Products;
using Grand.Business.Core.Interfaces.Checkout.CheckoutAttributes;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Business.Core.Utilities.Checkout;
using Grand.Domain.Catalog;
using Grand.Domain.Common;
using Grand.Domain.Customers;
using Grand.Domain.Orders;
using Grand.Infrastructure;
using Grand.Infrastructure.Validators;
using MediatR;

namespace Grand.Business.Checkout.Services.Orders;

public class ShoppingCartValidator : IShoppingCartValidator
{
    private readonly IMediator _mediator;
    private readonly IValidatorFactory _validatorFactory;
    private readonly ICheckoutAttributeService _checkoutAttributeService;
    private readonly ICheckoutAttributeParser _checkoutAttributeParser;
    private readonly ITranslationService _translationService;
    private readonly IContextAccessor _contextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly ShoppingCartSettings _shoppingCartSettings;
    private readonly IProductService _productService;
    private readonly IStockQuantityService _stockQuantityService;
    private readonly IAclService _aclService;
    private readonly IProductReservationService _productReservationService;


    public ShoppingCartValidator(
        IContextAccessor contextAccessor,
        IMediator mediator,
        IValidatorFactory validatorFactory,
        ICheckoutAttributeService checkoutAttributeService,
        ITranslationService translationService,
        ICheckoutAttributeParser checkoutAttributeParser,
        IPermissionService permissionService,
        ShoppingCartSettings shoppingCartSettings,
        IProductService productService,
        IStockQuantityService stockQuantityService,
        IAclService aclService,
        IProductReservationService productReservationService)
    {
        _contextAccessor = contextAccessor;
        _mediator = mediator;
        _validatorFactory = validatorFactory;
        _checkoutAttributeService = checkoutAttributeService;
        _translationService = translationService;
        _checkoutAttributeParser = checkoutAttributeParser;
        _permissionService = permissionService;
        _shoppingCartSettings = shoppingCartSettings;
        _productService = productService;
        _stockQuantityService = stockQuantityService;
        _aclService = aclService;
        _productReservationService = productReservationService;
    }

    public virtual async Task<IList<string>> GetStandardWarnings(Customer customer, Product product,
        ShoppingCartItem shoppingCartItem)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(product);

        var warnings = new List<string>();

        var result = await _validatorFactory.GetValidator<ShoppingCartStandardValidationContext>()
            .ValidateAsync(new ShoppingCartStandardValidationContext(customer, product, _aclService, shoppingCartItem));
        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));

        return warnings;
    }

    public virtual async Task<IList<string>> GetShoppingCartItemAttributeWarnings(Customer customer, Product product,
        ShoppingCartItem shoppingCartItem, bool ignoreNonCombinableAttributes = false)
    {
        ArgumentNullException.ThrowIfNull(product);

        var warnings = new List<string>();

        var result = await _validatorFactory.GetValidator<ShoppingCartItemAttributeValidationContext>().ValidateAsync(new ShoppingCartItemAttributeValidationContext(product, shoppingCartItem, ignoreNonCombinableAttributes));
        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));

        return warnings;
    }


    public virtual async Task<IList<string>> GetShoppingCartItemGiftVoucherWarnings(Customer customer,
        Product product, ShoppingCartItem shoppingCartItem)
    {
        ArgumentNullException.ThrowIfNull(product);

        var warnings = new List<string>();

        //gift vouchers
        if (!product.IsGiftVoucher) return warnings;

        GiftVoucherExtensions.GetGiftVoucherAttribute(shoppingCartItem.Attributes, out var giftVoucherRecipientName, out var giftVoucherRecipientEmail, out var giftVoucherSenderName, out var giftVoucherSenderEmail, out _);

        var context = new ShoppingCartGiftVoucherContext(giftVoucherRecipientName, giftVoucherRecipientEmail, giftVoucherSenderName, giftVoucherSenderEmail, product.GiftVoucherTypeId);
        var result = await _validatorFactory.GetValidator<ShoppingCartGiftVoucherContext>().ValidateAsync(context);

        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));

        return warnings;
    }

    public virtual async Task<IList<string>> GetInventoryProductWarnings(Customer customer, Product product,
        ShoppingCartItem shoppingCartItem)
    {
        var warnings = new List<string>();

        var bundleProductsWithProductTasks = product.BundleProducts.Select(async bundleProduct =>
        {
            var underlyingProduct = await _productService.GetProductById(bundleProduct.ProductId);
            return new BundleProductContext(bundleProduct, underlyingProduct);
        }).ToList();
        var bundleProductsWithProduct = await Task.WhenAll(bundleProductsWithProductTasks);
        var context = new ShoppingCartInventoryProductContext(customer, product, bundleProductsWithProduct, shoppingCartItem, _stockQuantityService, _shoppingCartSettings.AllowToSelectWarehouse);
        var result = await _validatorFactory.GetValidator<ShoppingCartInventoryProductContext>().ValidateAsync(context);
        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));

        return warnings;
    }


    public virtual async Task<IList<string>> GetAuctionProductWarning(double bid, Product product, Customer customer)
    {
        var warnings = new List<string>();
        if (product.ProductTypeId != ProductType.Auction) return warnings;
        var result = await _validatorFactory.GetValidator<ShoppingCartAuctionContext>().ValidateAsync(new ShoppingCartAuctionContext(product, bid));
        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));

        return warnings;
    }

    public virtual async Task<IList<string>> GetReservationProductWarnings(Customer customer, Product product,
        ShoppingCartItem shoppingCartItem)
    {
        var warnings = new List<string>();

        if (product.ProductTypeId != ProductType.Reservation)
            return warnings;

        var result = await _validatorFactory.GetValidator<ShoppingCartReservationProductValidationContext>()
            .ValidateAsync(new ShoppingCartReservationProductValidationContext(customer, product, shoppingCartItem, _productReservationService));
        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));

        return warnings;
    }

    public virtual async Task<IList<string>> GetShoppingCartWarnings(IList<ShoppingCartItem> shoppingCart,
        IList<CustomAttribute> checkoutAttributes, bool validateCheckoutAttributes, bool validateAmount)
    {
        var warnings = new List<string>();
        checkoutAttributes ??= new List<CustomAttribute>();

        var productTasks = shoppingCart.Select(async item => await _productService.GetProductById(item.ProductId));
        var products = await Task.WhenAll(productTasks);
        var result = await _validatorFactory.GetValidator<ShoppingCartWarningsValidationContext>().ValidateAsync(new ShoppingCartWarningsValidationContext(products));
        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));

        //validate checkout attributes
        if (validateCheckoutAttributes)
        {
            var allCheckoutAttributes = await _checkoutAttributeService.GetAllCheckoutAttributes(_contextAccessor.StoreContext.CurrentStore.Id, !shoppingCart.RequiresShipping());
            var parsedCheckoutAttributes = await _checkoutAttributeParser.ParseCheckoutAttributes(checkoutAttributes);
            var checkoutAttributesValidator = new ShoppingCartCheckoutAttributesValidator(_translationService);
            var resultCheckoutAttributes = await checkoutAttributesValidator.ValidateAsync(new ShoppingCartCheckoutAttributesContext(checkoutAttributes.AsReadOnly(), allCheckoutAttributes.AsReadOnly(), parsedCheckoutAttributes.AsReadOnly(), _checkoutAttributeParser));

            if (!resultCheckoutAttributes.IsValid)
                warnings.AddRange(resultCheckoutAttributes.Errors.Select(x => x.ErrorMessage));
        }

        //validate subtotal/total amount in the cart
        if (validateAmount)
        {
            var resultCheckoutAttributes = await _validatorFactory
                .GetValidator<ShoppingCartTotalAmountValidatorRecord>().ValidateAsync(
                    new ShoppingCartTotalAmountValidatorRecord(_contextAccessor.WorkContext.CurrentCustomer,
                        _contextAccessor.WorkContext.WorkingCurrency, shoppingCart));
            if (!resultCheckoutAttributes.IsValid)
                warnings.AddRange(resultCheckoutAttributes.Errors.Select(x => x.ErrorMessage));
        }

        //event notification
        await _mediator.ShoppingCartWarningsAdd(warnings, shoppingCart, checkoutAttributes, validateCheckoutAttributes);

        return warnings;
    }

    public async Task<IList<string>> CheckCommonWarnings(Customer customer, IList<ShoppingCartItem> currentCart,
        Product product,
        ShoppingCartType shoppingCartType, DateTime? rentalStartDate, DateTime? rentalEndDate,
        int quantity, string reservationId)
    {
        var warnings = new List<string>();

        var result = await _validatorFactory.GetValidator<ShoppingCartCommonWarningsValidationContext>().ValidateAsync(new ShoppingCartCommonWarningsValidationContext(customer, currentCart.AsReadOnly(), shoppingCartType, quantity, _shoppingCartSettings, _permissionService));
        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));

        return warnings;
    }

    public virtual async Task<IList<string>> GetShoppingCartItemWarnings(Customer customer,
        ShoppingCartItem shoppingCartItem,
        Product product, ShoppingCartValidatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(product);

        var warnings = new List<string>();

        //standard properties
        if (options.GetStandardWarnings)
            warnings.AddRange(await GetStandardWarnings(customer, product, shoppingCartItem));

        //inventory properties
        if (options.GetInventoryWarnings)
            warnings.AddRange(await GetInventoryProductWarnings(customer, product, shoppingCartItem));

        //selected attributes
        if (options.GetAttributesWarnings)
            warnings.AddRange(await GetShoppingCartItemAttributeWarnings(customer, product, shoppingCartItem));

        //gift vouchers
        if (options.GetGiftVoucherWarnings)
            warnings.AddRange(await GetShoppingCartItemGiftVoucherWarnings(customer, product, shoppingCartItem));

        //required products
        if (options.GetRequiredProductWarnings)
            warnings.AddRange(await GetRequiredProductWarnings(customer, shoppingCartItem, product,
                shoppingCartItem.StoreId));

        //reservation products
        if (options.GetReservationWarnings)
            warnings.AddRange(await GetReservationProductWarnings(customer, product, shoppingCartItem));

        //event notification
        await _mediator.ShoppingCartItemWarningsAdded(warnings, customer, shoppingCartItem, product);

        return warnings;
    }

    public virtual async Task<IList<string>> GetRequiredProductWarnings(Customer customer,
        ShoppingCartItem shoppingCartItem, Product product, string storeId)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(product);

        var warnings = new List<string>();

        if (!product.RequireOtherProducts) return warnings;

        var cart = customer.ShoppingCartItems
            .Where(sci => sci.ShoppingCartTypeId == shoppingCartItem.ShoppingCartTypeId)
            .LimitPerStore(_shoppingCartSettings.SharedCartBetweenStores, storeId)
            .ToList();

        var requiredProductsTasks = product.ParseRequiredProductIds().Select(async productId => await _productService.GetProductById(productId));
        var requiredProducts = await Task.WhenAll(requiredProductsTasks);

        var result = await _validatorFactory.GetValidator<ShoppingCartRequiredProductsValidationContext>().ValidateAsync(new ShoppingCartRequiredProductsValidationContext(cart, requiredProducts));
        if (!result.IsValid)
            warnings.AddRange(result.Errors.Select(x => x.ErrorMessage));
        return warnings;
    }
}