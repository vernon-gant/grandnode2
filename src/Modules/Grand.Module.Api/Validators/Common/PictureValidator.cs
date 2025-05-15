using FluentValidation;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Infrastructure.Validators;
using Grand.Module.Api.DTOs.Common;

namespace Grand.Module.Api.Validators.Common;

public class PictureValidator : BaseGrandValidator<PictureDto>
{
    public PictureValidator(
        IEnumerable<IValidatorConsumer<PictureDto>> validators,
        ITranslationService translationService)
        : base(validators)
    {
        RuleFor(x => x.MimeType).NotEmpty()
            .WithMessage(translationService.GetResource("Api.Common.Picture.Fields.MimeType.Required"));
        RuleFor(x => x.PictureBinary).Must((x, _) =>
        {
            if (!string.IsNullOrEmpty(x.Id) && (x.PictureBinary == null || x.PictureBinary.Length == 0)) return true;
            return !string.IsNullOrEmpty(x.Id) || (x.PictureBinary != null && x.PictureBinary.Length != 0);
        }).WithMessage(translationService.GetResource("Api.Common.Picture.Fields.PictureBinary.Required"));
    }
}