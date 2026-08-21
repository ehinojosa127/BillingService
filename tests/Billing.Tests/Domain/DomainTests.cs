using Billing.Domain.Catalogs;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Domain.Exceptions;
using Billing.Domain.Services;
using Billing.Domain.ValueObjects;

namespace Billing.Tests.Domain;

public sealed class RucTests
{
    [Fact]
    public void Valid_official_check_digit_is_accepted()
    {
        var ruc = new Ruc("20100070970");
        Assert.Equal("20100070970", ruc.Value);
    }

    [Fact]
    public void Invalid_check_digit_is_rejected()
    {
        Assert.Throws<BusinessRuleException>(() => new Ruc("20100070971"));
    }
}

public sealed class TaxRegimeTests
{
    [Fact]
    public void Rus_allows_only_receipts()
    {
        Assert.True(TaxRegime.Rus.CanIssue(DocumentType.Receipt));
        Assert.False(TaxRegime.Rus.CanIssue(DocumentType.Invoice));
        Assert.Throws<BusinessRuleException>(() => TaxRegime.Rus.EnsureCanIssue(DocumentType.Invoice));
    }

    [Fact]
    public void Rer_and_above_allow_invoices()
    {
        Assert.True(TaxRegime.Rer.CanIssue(DocumentType.Invoice));
        Assert.True(TaxRegime.Mype.CanIssue(DocumentType.Invoice));
        Assert.True(TaxRegime.General.CanIssue(DocumentType.Invoice));
    }

    [Fact]
    public void Dash_fiscal_address_is_normalized()
    {
        var address = new Address("-", "040101", "AREQUIPA", "AREQUIPA", "AREQUIPA");
        Assert.Equal("S/N", address.Line);
    }
}

public sealed class TaxCalculatorTests
{
    [Fact]
    public void Gravado_line_applies_eighteen_percent_igv()
    {
        var result = TaxCalculator.CalculateLine(2, 100m, 0m, TaxAffectationCode.GravadoOnerosa, "PEN");
        Assert.Equal(200m, result.LineExtensionAmount);
        Assert.Equal(36m, result.IgvAmount);
        Assert.Equal(118m, result.UnitPrice);
        Assert.Equal(236m, result.Total);
    }

    [Fact]
    public void Tax_inclusive_price_keeps_payable_amount()
    {
        var result = TaxCalculator.CalculateLine(
            1,
            10m / 1.18m,
            0m,
            TaxAffectationCode.GravadoOnerosa,
            "PEN");

        Assert.Equal(10m, result.Total);
        Assert.Equal(10m, result.UnitPrice);
        Assert.Equal(8.47m, result.LineExtensionAmount);
        Assert.Equal(1.53m, result.IgvAmount);
    }

    [Fact]
    public void Stored_unit_value_keeps_tax_inclusive_total()
    {
        var item = DocumentItem.Create(
            Guid.NewGuid(),
            1,
            "1",
            "Producto",
            1,
            "NIU",
            10m / 1.18m,
            0m,
            TaxAffectationCode.GravadoOnerosa,
            "PEN");

        Assert.Equal(10m, item.Total);
        var totals = TaxCalculator.CalculateDocument(
            [new DocumentLineInput(item.Quantity, item.UnitValue, item.Discount, item.Affectation)],
            "PEN");
        Assert.Equal(10m, totals.PayableAmount);
    }

    [Fact]
    public void Document_totals_split_tax_buckets()
    {
        var totals = TaxCalculator.CalculateDocument(
        [
            new DocumentLineInput(2, 100m, 0m, TaxAffectationCode.GravadoOnerosa),
            new DocumentLineInput(2, 50m, 0m, TaxAffectationCode.ExoneradoOnerosa)
        ], "PEN");

        Assert.Equal(200m, totals.TaxableAmount);
        Assert.Equal(100m, totals.ExemptAmount);
        Assert.Equal(36m, totals.IgvAmount);
        Assert.Equal(336m, totals.PayableAmount);
    }
}

public sealed class DocumentStatusMachineTests
{
    [Fact]
    public void Allows_known_transitions_and_rejects_the_rest()
    {
        Assert.Equal(DocumentStatus.Generated, DocumentStatusMachine.Transition(DocumentStatus.Draft, DocumentStatus.Generated));
        Assert.Throws<InvalidStatusTransitionException>(() =>
            DocumentStatusMachine.Transition(DocumentStatus.Accepted, DocumentStatus.Draft));
    }
}

public sealed class ElectronicDocumentTests
{
    [Fact]
    public void Invoice_requires_ruc_and_calculates_totals()
    {
        var document = DocumentFactory.Invoice();
        Assert.Equal("01", document.DocumentTypeCode);
        Assert.Equal("F001-00001", document.FullNumber);
        Assert.Equal($"{document.IssuerRuc}-01-F001-00001", document.XmlFileName);
        Assert.Equal(118m, document.PayableAmount);
        Assert.Equal(DocumentStatus.Draft, document.Status);
        Assert.Contains("SOLES", document.AmountInWords);
    }

    [Fact]
    public void Invoice_with_dni_is_rejected()
    {
        Assert.Throws<BusinessRuleException>(() => DocumentFactory.Invoice(recipientType: IdentityDocumentType.Dni, recipientNumber: "12345678"));
    }

    [Fact]
    public void Credit_note_requires_related_document_and_matching_series()
    {
        var note = DocumentFactory.CreditNote();
        Assert.Single(note.References);
        Assert.Equal("07", note.DocumentTypeCode);
    }

    [Fact]
    public void External_reference_stores_entity_and_id_without_becoming_a_foreign_key()
    {
        var document = DocumentFactory.Invoice();
        Assert.Equal("test-erp", document.ExternalSystem);
        Assert.Equal("order", document.ExternalEntity);
        Assert.Equal("42", document.ExternalId);
        Assert.Equal("ORD-1", document.ExternalReference);
    }

    [Fact]
    public void Tax_rejection_is_not_retryable_but_transient_failure_is()
    {
        var rejected = DocumentFactory.Invoice();
        rejected.MarkGenerated(DateTimeOffset.UtcNow);
        rejected.MarkSigned("digest", DateTimeOffset.UtcNow);
        var submission = rejected.StartSubmission(DateTimeOffset.UtcNow);
        rejected.ApplySunatResult(submission, SunatStatus.Rejected, "2324", "El documento no cumple", null, null, null, DateTimeOffset.UtcNow);
        Assert.False(DocumentStatusMachine.CanRetrySubmission(rejected));

        var failed = DocumentFactory.Invoice(number: 2);
        failed.MarkGenerated(DateTimeOffset.UtcNow);
        failed.MarkSigned("digest", DateTimeOffset.UtcNow);
        failed.MarkFailed("TransientCommunicationError", "timeout", DateTimeOffset.UtcNow);
        Assert.True(DocumentStatusMachine.CanRetrySubmission(failed));
        Assert.True(DocumentStatusMachine.CanConsult(failed));

        var draft = DocumentFactory.Invoice(number: 3);
        Assert.True(DocumentStatusMachine.CanRetrySubmission(draft));
        Assert.True(DocumentStatusMachine.CanCancel(draft));
        Assert.False(DocumentStatusMachine.CanConsult(draft));
    }

    [Fact]
    public void Accepted_document_requires_sunat_void_until_cancelled()
    {
        var document = DocumentFactory.Invoice(number: 5);
        document.MarkGenerated(DateTimeOffset.UtcNow);
        document.MarkSigned("digest", DateTimeOffset.UtcNow);
        var submission = document.StartSubmission(DateTimeOffset.UtcNow);
        document.ApplySunatResult(submission, SunatStatus.Accepted, "0", "Aceptada", null, null, null, DateTimeOffset.UtcNow);

        Assert.True(DocumentStatusMachine.CanCancel(document));
        Assert.True(DocumentStatusMachine.RequiresSunatVoid(document));
        Assert.False(DocumentStatusMachine.CanConsult(document));
        Assert.False(DocumentStatusMachine.CanRetrySubmission(document));

        document.Cancel(DateTimeOffset.UtcNow);

        Assert.False(DocumentStatusMachine.CanCancel(document));
        Assert.False(DocumentStatusMachine.CanConsult(document));
    }

    [Fact]
    public void Test_simulation_codes_are_ignored_in_production()
    {
        Assert.Equal(
            BillingTestSimulationMode.None,
            BillingTestSimulation.Resolve("#TEST:REJECTED", isProductionEnvironment: true));
        Assert.Equal(
            BillingTestSimulationMode.Rejected,
            BillingTestSimulation.Resolve("#TEST:REJECTED", isProductionEnvironment: false));
        Assert.Equal(
            BillingTestSimulationMode.Pending,
            BillingTestSimulation.Resolve("Entrega urgente #TEST:PENDING", isProductionEnvironment: false));
    }

    [Fact]
    public void Test_simulation_sanitizes_observation_for_documents()
    {
        var sanitized = BillingTestSimulation.SanitizeObservation(
            "Pedido showroom #TEST:REJECTED",
            isProductionEnvironment: false);

        Assert.Equal("Pedido showroom", sanitized);
    }

    [Fact]
    public void Accepted_document_cannot_be_marked_failed()
    {
        var document = DocumentFactory.Invoice(number: 4);
        document.MarkGenerated(DateTimeOffset.UtcNow);
        document.MarkSigned("digest", DateTimeOffset.UtcNow);
        var submission = document.StartSubmission(DateTimeOffset.UtcNow);
        document.ApplySunatResult(submission, SunatStatus.Accepted, "0", "Aceptada", null, null, null, DateTimeOffset.UtcNow);

        document.MarkFailed("InternalError", "should be ignored", DateTimeOffset.UtcNow);

        Assert.Equal(DocumentStatus.Accepted, document.Status);
        Assert.Equal(SunatStatus.Accepted, document.SunatStatus);
    }
}

public static class DocumentFactory
{
    public static Issuer Issuer() =>
        Billing.Domain.Entities.Issuer.Create(
            "20100070970",
            "EMISOR DE PRUEBA S.A.C.",
            "EMISOR",
            new Address("AV PRINCIPAL 123", "150101", "LIMA", "LIMA", "LIMA"),
            "billing@example.com",
            "014445555",
            "0000",
            DateTimeOffset.UtcNow);

    public static ElectronicDocument Invoice(
        IdentityDocumentType? recipientType = null,
        string? recipientNumber = null,
        int number = 1)
    {
        return ElectronicDocument.Issue(
            Issuer(),
            DocumentType.Invoice,
            "F001",
            number,
            new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.FromHours(-5)),
            CurrencyCode.Pen,
            OperationTypeCode.InternalSale,
            PaymentForm.Cash,
            new IdentityDocument(recipientType ?? IdentityDocumentType.Ruc, recipientNumber ?? "20000000001"),
            "CLIENTE S.A.C.",
            "AV CLIENTE 456",
            null,
            [new DocumentItemDraft("P01", "Producto de prueba", 1, "NIU", 100m, 0m, TaxAffectationCode.GravadoOnerosa)],
            null,
            null,
            new ExternalReference("test-erp", "ORD-1", "order", "42"),
            "tester",
            null,
            null,
            0m,
            0m);
    }

    public static ElectronicDocument Receipt() =>
        ElectronicDocument.Issue(
            Issuer(),
            DocumentType.Receipt,
            "B001",
            1,
            new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.FromHours(-5)),
            CurrencyCode.Pen,
            OperationTypeCode.InternalSale,
            PaymentForm.Cash,
            new IdentityDocument(IdentityDocumentType.Dni, "12345678"),
            "JUAN PEREZ",
            null,
            null,
            [new DocumentItemDraft("P01", "Producto de prueba", 1, "NIU", 100m, 0m, TaxAffectationCode.GravadoOnerosa)],
            null,
            null,
            null,
            null,
            null,
            null,
            0m,
            0m);

    public static ElectronicDocument CreditNote() =>
        ElectronicDocument.Issue(
            Issuer(),
            DocumentType.CreditNote,
            "F001",
            2,
            new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.FromHours(-5)),
            CurrencyCode.Pen,
            OperationTypeCode.InternalSale,
            PaymentForm.Cash,
            new IdentityDocument(IdentityDocumentType.Ruc, "20000000001"),
            "CLIENTE S.A.C.",
            null,
            null,
            [new DocumentItemDraft("P01", "Producto de prueba", 1, "NIU", 100m, 0m, TaxAffectationCode.GravadoOnerosa)],
            new RelatedDocument(DocumentType.Invoice, "F001", 1, NoteReasonCode.CreditCancellation),
            null,
            null,
            null,
            null,
            null,
            0m,
            0m);

    public static ElectronicDocument DebitNote() =>
        ElectronicDocument.Issue(
            Issuer(),
            DocumentType.DebitNote,
            "F001",
            3,
            new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.FromHours(-5)),
            CurrencyCode.Pen,
            OperationTypeCode.InternalSale,
            PaymentForm.Cash,
            new IdentityDocument(IdentityDocumentType.Ruc, "20000000001"),
            "CLIENTE S.A.C.",
            null,
            null,
            [new DocumentItemDraft("P01", "Producto de prueba", 1, "NIU", 20m, 0m, TaxAffectationCode.GravadoOnerosa)],
            new RelatedDocument(DocumentType.Invoice, "F001", 1, NoteReasonCode.DebitIncrease),
            null,
            null,
            null,
            null,
            null,
            0m,
            0m);
}
