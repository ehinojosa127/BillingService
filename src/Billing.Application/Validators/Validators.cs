using Billing.Application.Commands;
using Billing.Domain.Catalogs;
using FluentValidation;

namespace Billing.Application.Validators;

public sealed class IssueDocumentCommandValidator : AbstractValidator<IssueDocumentCommand>
{
    public IssueDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty().Must(code => DocumentType.TryFromCode(code, out _));
        RuleFor(x => x.Series).NotEmpty().Length(4);
        RuleFor(x => x.RecipientIdentityType).NotEmpty();
        RuleFor(x => x.RecipientIdentityNumber).NotEmpty();
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new IssueItemDtoValidator());
        RuleFor(x => x.GlobalDiscount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GlobalCharge).GreaterThanOrEqualTo(0);

        When(x => DocumentType.TryFromCode(x.DocumentType, out var type) && type == DocumentType.Invoice, () =>
        {
            RuleFor(x => x.RecipientIdentityType).Equal(IdentityDocumentType.Ruc.Code);
        });

        When(x => DocumentType.TryFromCode(x.DocumentType, out var type) && type.IsNote, () =>
        {
            RuleFor(x => x.RelatedDocument).NotNull();
            When(x => x.RelatedDocument is not null, () =>
            {
                RuleFor(x => x.RelatedDocument!.DocumentType).NotEmpty();
                RuleFor(x => x.RelatedDocument!.Series).NotEmpty();
                RuleFor(x => x.RelatedDocument!.Number).GreaterThan(0);
                RuleFor(x => x.RelatedDocument!.ReasonCode).NotEmpty();
            });
        });

        When(x => DocumentType.TryFromCode(x.DocumentType, out var type) && type.IsShippingGuide, () =>
        {
            RuleFor(x => x.ShippingGuide).NotNull();
            RuleFor(x => x.ShippingGuide!.TransferReason).NotEmpty();
            RuleFor(x => x.ShippingGuide!.TransportMode).NotEmpty();
            RuleFor(x => x.ShippingGuide!.GrossWeightKg).GreaterThan(0);
            RuleFor(x => x.ShippingGuide!.PackageCount).GreaterThan(0);
            RuleFor(x => x.ShippingGuide!.Origin).NotNull();
            RuleFor(x => x.ShippingGuide!.Destination).NotNull();
        });
    }
}

public sealed class IssueItemDtoValidator : AbstractValidator<IssueItemDto>
{
    public IssueItemDtoValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCode).NotEmpty();
        RuleFor(x => x.UnitValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxAffectation).NotEmpty();
    }
}

public sealed class UpsertIssuerCommandValidator : AbstractValidator<UpsertIssuerCommand>
{
    public UpsertIssuerCommandValidator()
    {
        RuleFor(x => x.Ruc).NotEmpty().Length(11);
        RuleFor(x => x.LegalName).NotEmpty();
        RuleFor(x => x.AddressLine).NotEmpty();
        RuleFor(x => x.Ubigeo).NotEmpty().Length(6);
        RuleFor(x => x.Department).NotEmpty();
        RuleFor(x => x.Province).NotEmpty();
        RuleFor(x => x.District).NotEmpty();
    }
}

public sealed class CreateSeriesCommandValidator : AbstractValidator<CreateSeriesCommand>
{
    public CreateSeriesCommandValidator()
    {
        RuleFor(x => x.DocumentType).NotEmpty();
        RuleFor(x => x.Series).NotEmpty().Length(4);
    }
}
