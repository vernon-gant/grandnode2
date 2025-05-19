using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Catalog;
using Grand.SharedKernel.Extensions;
using System.Linq.Expressions;

namespace Grand.Business.Checkout.Validators;

/// In the context of validating a gift voucher in the shopping cart, the system must ensure that:
/// 1. The recipient name must not be empty.
/// 2. The recipient email must not be empty and must be valid when the voucher is virtual.
/// 3. The sender name must not be empty.
/// 4. The sender email must not be empty and must be valid when the gift voucher type is virtual.
public record ShoppingCartGiftVoucherContext(string RecipientName, string RecipientEmail, string SenderName, string SenderEmail, GiftVoucherType VoucherType);

public class ShoppingCartGiftVoucherValidator : AbstractValidator<ShoppingCartGiftVoucherContext>
{
    private readonly ITranslationService _translationService;

    public ShoppingCartGiftVoucherValidator(ITranslationService translationService)
    {
        _translationService = translationService;

        RuleFor(RecipientName).NotEmpty().WithMessage(RecipientNameErrorMessage);

        RuleFor(RecipientEmail).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(RecipientEmailErrorMessage).Must(CommonHelper.IsValidEmail).WithMessage(RecipientEmailErrorMessage).When(IsVirtualVoucher);

        RuleFor(SenderName).NotEmpty().WithMessage(SenderNameErrorMessage);

        RuleFor(SenderEmail).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(SenderEmailErrorMessage).Must(CommonHelper.IsValidEmail).WithMessage(SenderEmailErrorMessage).When(IsVirtualVoucher).When(IsVirtualVoucher);
    }

    private static readonly Expression<Func<ShoppingCartGiftVoucherContext, string>> RecipientName = context => context.RecipientName;

    private static readonly Expression<Func<ShoppingCartGiftVoucherContext, string>> RecipientEmail = context => context.RecipientEmail;

    private static readonly Expression<Func<ShoppingCartGiftVoucherContext, string>> SenderName = context => context.SenderName;

    private static readonly Expression<Func<ShoppingCartGiftVoucherContext, string>> SenderEmail = context => context.SenderEmail;


    private static bool IsVirtualVoucher(ShoppingCartGiftVoucherContext context) => context.VoucherType == GiftVoucherType.Virtual;

    private string RecipientNameErrorMessage(ShoppingCartGiftVoucherContext context) => _translationService.GetResource("ShoppingCart.RecipientNameError");

    private string RecipientEmailErrorMessage(ShoppingCartGiftVoucherContext context) => _translationService.GetResource("ShoppingCart.RecipientEmailError");

    private string SenderNameErrorMessage(ShoppingCartGiftVoucherContext context) => _translationService.GetResource("ShoppingCart.SenderNameError");

    private string SenderEmailErrorMessage(ShoppingCartGiftVoucherContext context) => _translationService.GetResource("ShoppingCart.SenderEmailError");
}