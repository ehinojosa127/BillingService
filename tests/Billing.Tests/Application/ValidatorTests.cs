using Billing.Application.Commands;
using Billing.Application.Validators;
using Billing.Domain.Catalogs;
using FluentValidation.TestHelper;

namespace Billing.Tests.Application;

public sealed class IssueDocumentCommandValidatorTests
{
    private readonly IssueDocumentCommandValidator _validator = new();

    [Fact]
    public void Invoice_without_items_is_invalid()
    {
        var command = ValidInvoice() with { Items = [] };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void Invoice_requires_ruc()
    {
        var command = ValidInvoice() with { RecipientIdentityType = IdentityDocumentType.Dni.Code };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RecipientIdentityType);
    }

    [Fact]
    public void Credit_note_requires_related_document()
    {
        var command = ValidInvoice() with { DocumentType = DocumentType.CreditNote.Code, RelatedDocument = null };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RelatedDocument);
    }

    private static IssueDocumentCommand ValidInvoice() => new()
    {
        DocumentType = "01",
        Series = "F001",
        RecipientIdentityType = "6",
        RecipientIdentityNumber = "20123456789",
        RecipientName = "CLIENTE S.A.C.",
        Items =
        [
            new IssueItemDto("P01", "Producto", 1, "NIU", 100m, 0m, "10")
        ]
    };
}
