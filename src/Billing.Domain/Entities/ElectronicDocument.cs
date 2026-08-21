using Billing.Domain.Catalogs;
using Billing.Domain.Enums;
using Billing.Domain.Exceptions;
using Billing.Domain.Services;
using Billing.Domain.ValueObjects;

namespace Billing.Domain.Entities;

public sealed class ElectronicDocument
{
    private readonly List<DocumentItem> _items = [];
    private readonly List<DocumentReference> _references = [];
    private readonly List<DocumentSubmission> _submissions = [];
    private readonly List<GeneratedFile> _files = [];

    public Guid Id { get; private set; }
    public string DocumentTypeCode { get; private set; } = string.Empty;
    public string Series { get; private set; } = string.Empty;
    public int Number { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public TimeOnly IssueTime { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Pen.Code;
    public string OperationTypeCode { get; private set; } = Catalogs.OperationTypeCode.InternalSale.Code;
    public PaymentForm PaymentForm { get; private set; }
    public DocumentStatus Status { get; private set; }
    public SunatStatus SunatStatus { get; private set; }
    public string IssuerRuc { get; private set; } = string.Empty;
    public string IssuerLegalName { get; private set; } = string.Empty;
    public string IssuerTradeName { get; private set; } = string.Empty;
    public string IssuerAddressLine { get; private set; } = string.Empty;
    public string IssuerUbigeo { get; private set; } = string.Empty;
    public string IssuerDepartment { get; private set; } = string.Empty;
    public string IssuerProvince { get; private set; } = string.Empty;
    public string IssuerDistrict { get; private set; } = string.Empty;
    public string IssuerCountryCode { get; private set; } = "PE";
    public string? IssuerUrbanization { get; private set; }
    public string IssuerEstablishmentCode { get; private set; } = "0000";
    public string? IssuerEmail { get; private set; }
    public string? IssuerPhone { get; private set; }
    public string RecipientIdentityType { get; private set; } = IdentityDocumentType.Ruc.Code;
    public string RecipientIdentityNumber { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string? RecipientAddressLine { get; private set; }
    public string? RecipientEmail { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal ExemptAmount { get; private set; }
    public decimal UnaffectedAmount { get; private set; }
    public decimal FreeAmount { get; private set; }
    public decimal ExportAmount { get; private set; }
    public decimal IgvAmount { get; private set; }
    public decimal LineExtensionAmount { get; private set; }
    public decimal TaxInclusiveAmount { get; private set; }
    public decimal PayableAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal ChargeAmount { get; private set; }
    public string AmountInWords { get; private set; } = string.Empty;
    public string? DigestValue { get; private set; }
    public string? Observation { get; private set; }
    public string? ExternalSystem { get; private set; }
    public string? ExternalEntity { get; private set; }
    public string? ExternalId { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? RequestedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public string? TransferReasonCode { get; private set; }
    public string? TransportModeCode { get; private set; }
    public DateOnly? TransferStartDate { get; private set; }
    public decimal? GrossWeightKg { get; private set; }
    public int? PackageCount { get; private set; }
    public string? OriginAddressLine { get; private set; }
    public string? OriginUbigeo { get; private set; }
    public string? OriginDepartment { get; private set; }
    public string? OriginProvince { get; private set; }
    public string? OriginDistrict { get; private set; }
    public string? DestinationAddressLine { get; private set; }
    public string? DestinationUbigeo { get; private set; }
    public string? DestinationDepartment { get; private set; }
    public string? DestinationProvince { get; private set; }
    public string? DestinationDistrict { get; private set; }
    public string? CarrierRuc { get; private set; }
    public string? CarrierName { get; private set; }
    public string? VehiclePlate { get; private set; }
    public string? DriverLicense { get; private set; }
    public string? DriverDocumentType { get; private set; }
    public string? DriverDocumentNumber { get; private set; }

    public IReadOnlyCollection<DocumentItem> Items => _items;
    public IReadOnlyCollection<DocumentReference> References => _references;
    public IReadOnlyCollection<DocumentSubmission> Submissions => _submissions;
    public IReadOnlyCollection<GeneratedFile> Files => _files;

    private ElectronicDocument()
    {
    }

    public static ElectronicDocument Issue(
        Issuer issuer,
        DocumentType documentType,
        string series,
        int number,
        DateTimeOffset issuedAt,
        CurrencyCode currency,
        OperationTypeCode operationType,
        PaymentForm paymentForm,
        IdentityDocument recipientIdentity,
        string recipientName,
        string? recipientAddress,
        string? recipientEmail,
        IReadOnlyList<DocumentItemDraft> items,
        RelatedDocument? relatedDocument,
        ShippingGuideInfo? shippingGuide,
        ExternalReference? externalReference,
        string? requestedBy,
        string? observation,
        DateOnly? dueDate,
        decimal globalDiscount,
        decimal globalCharge)
    {
        ValidateIssue(documentType, series, recipientIdentity, relatedDocument, shippingGuide, items);

        var document = new ElectronicDocument
        {
            Id = Guid.CreateVersion7(),
            DocumentTypeCode = documentType.Code,
            Series = new DocumentSeriesCode(series).Value,
            Number = new DocumentNumber(number).Value,
            IssueDate = DateOnly.FromDateTime(issuedAt.DateTime),
            IssueTime = TimeOnly.FromDateTime(issuedAt.DateTime),
            DueDate = dueDate,
            Currency = currency.Code,
            OperationTypeCode = operationType.Code,
            PaymentForm = paymentForm,
            Status = DocumentStatus.Draft,
            SunatStatus = SunatStatus.NotSent,
            IssuerRuc = issuer.Ruc,
            IssuerLegalName = issuer.LegalName,
            IssuerTradeName = issuer.TradeName,
            IssuerAddressLine = issuer.Address.Line,
            IssuerUbigeo = issuer.Address.Ubigeo,
            IssuerDepartment = issuer.Address.Department,
            IssuerProvince = issuer.Address.Province,
            IssuerDistrict = issuer.Address.District,
            IssuerCountryCode = issuer.Address.CountryCode,
            IssuerUrbanization = issuer.Address.Urbanization,
            IssuerEstablishmentCode = issuer.EstablishmentCode,
            IssuerEmail = issuer.Email,
            IssuerPhone = issuer.Phone,
            RecipientIdentityType = recipientIdentity.Type.Code,
            RecipientIdentityNumber = recipientIdentity.Number,
            RecipientName = recipientName.Trim(),
            RecipientAddressLine = string.IsNullOrWhiteSpace(recipientAddress) ? null : recipientAddress.Trim(),
            RecipientEmail = string.IsNullOrWhiteSpace(recipientEmail) ? null : recipientEmail.Trim(),
            Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim(),
            ExternalSystem = TrimToNull(externalReference?.System),
            ExternalEntity = TrimToNull(externalReference?.Entity),
            ExternalId = TrimToNull(externalReference?.Id),
            ExternalReference = TrimToNull(externalReference?.Reference),
            RequestedBy = requestedBy,
            CreatedAt = issuedAt.ToUniversalTime(),
            UpdatedAt = issuedAt.ToUniversalTime()
        };

        if (relatedDocument is not null)
        {
            document._references.Add(DocumentReference.FromRelated(document.Id, relatedDocument));
        }

        if (shippingGuide is not null)
        {
            document.ApplyShippingGuide(shippingGuide);
        }

        var lineNumber = 1;
        foreach (var draft in items)
        {
            document._items.Add(DocumentItem.Create(
                document.Id,
                lineNumber++,
                draft.Code,
                draft.Description,
                draft.Quantity,
                draft.UnitCode,
                draft.UnitValue,
                draft.Discount,
                draft.Affectation,
                currency.Code));
        }

        document.RecalculateTotals(globalDiscount, globalCharge);
        return document;
    }

    public DocumentType Type => DocumentType.FromCode(DocumentTypeCode);
    public string FullNumber => DocumentNumberFormat.Combine(Series, Number);
    /// <summary>
    /// Nombre SUNAT del XML/ZIP: {RUC}-{Tipo}-{Serie}-{Correlativo}.
    /// El correlativo debe coincidir exactamente con la parte numérica de <c>cbc:ID</c> (FullNumber).
    /// </summary>
    public string XmlFileName =>
        $"{IssuerRuc}-{DocumentTypeCode}-{Series}-{DocumentNumberFormat.FormatNumber(Number)}";

    public void MarkGenerated(DateTimeOffset now)
    {
        Status = DocumentStatusMachine.Transition(Status, DocumentStatus.Generated);
        UpdatedAt = now.ToUniversalTime();
    }

    public void MarkSigned(string digestValue, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(digestValue))
        {
            throw new BusinessRuleException("SIGNATURE", "Digest value is required after signing.");
        }

        Status = DocumentStatusMachine.Transition(Status, DocumentStatus.Signed);
        DigestValue = digestValue;
        UpdatedAt = now.ToUniversalTime();
    }

    public DocumentSubmission RecordAuxiliarySubmission(DateTimeOffset now)
    {
        UpdatedAt = now.ToUniversalTime();
        var submission = DocumentSubmission.Start(Id, _submissions.Count + 1, now);
        _submissions.Add(submission);
        return submission;
    }

    public DocumentSubmission StartSubmission(DateTimeOffset now)
    {
        if (Status != DocumentStatus.Sent)
        {
            Status = DocumentStatusMachine.Transition(Status, DocumentStatus.Sent);
        }

        SunatStatus = SunatStatus.Pending;
        UpdatedAt = now.ToUniversalTime();
        var submission = DocumentSubmission.Start(Id, _submissions.Count + 1, now);
        _submissions.Add(submission);
        return submission;
    }

    public void ApplySunatResult(
        DocumentSubmission submission,
        SunatStatus sunatStatus,
        string? responseCode,
        string? description,
        string? notes,
        string? ticket,
        string? errorKind,
        DateTimeOffset now)
    {
        submission.Complete(sunatStatus, now, ticket, responseCode, description, notes, errorKind);
        SunatStatus = sunatStatus;
        UpdatedAt = now.ToUniversalTime();

        Status = sunatStatus switch
        {
            SunatStatus.Accepted => DocumentStatusMachine.Transition(Status, DocumentStatus.Accepted),
            SunatStatus.AcceptedWithObservations => DocumentStatusMachine.Transition(Status, DocumentStatus.Observed),
            SunatStatus.Rejected => DocumentStatusMachine.Transition(Status, DocumentStatus.Rejected),
            SunatStatus.InProcess => Status,
            _ => DocumentStatusMachine.Transition(Status, DocumentStatus.Failed)
        };
    }

    public void MarkFailed(string errorKind, string description, DateTimeOffset now)
    {
        if (Status is DocumentStatus.Accepted or DocumentStatus.Observed or DocumentStatus.Rejected or DocumentStatus.Cancelled)
        {
            return;
        }

        if (Status != DocumentStatus.Failed)
        {
            Status = DocumentStatusMachine.Transition(Status, DocumentStatus.Failed);
        }

        SunatStatus = SunatStatus.CommunicationError;
        UpdatedAt = now.ToUniversalTime();
        if (_submissions.Count > 0)
        {
            var last = _submissions[^1];
            last.Complete(SunatStatus.CommunicationError, now, last.Ticket, null, description, null, errorKind);
        }
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == DocumentStatus.Cancelled)
        {
            return;
        }

        Status = DocumentStatusMachine.Transition(Status, DocumentStatus.Cancelled);
        UpdatedAt = now.ToUniversalTime();
    }

    public void ReconcileFromSunat(
        SunatStatus sunatStatus,
        string? responseCode,
        string? description,
        string? notes,
        string? ticket,
        DateTimeOffset now)
    {
        if (Status == DocumentStatus.Cancelled)
        {
            return;
        }

        if (_submissions.Count == 0)
        {
            _submissions.Add(DocumentSubmission.Start(Id, 1, now));
        }

        var last = _submissions[^1];
        last.Complete(sunatStatus, now, ticket ?? last.Ticket, responseCode, description, notes, last.ErrorKind);
        SunatStatus = sunatStatus;
        UpdatedAt = now.ToUniversalTime();

        var target = sunatStatus switch
        {
            SunatStatus.Accepted => DocumentStatus.Accepted,
            SunatStatus.AcceptedWithObservations => DocumentStatus.Observed,
            SunatStatus.Rejected => DocumentStatus.Rejected,
            SunatStatus.InProcess or SunatStatus.Pending => DocumentStatus.Sent,
            _ => Status
        };

        if (Status != target && (DocumentStatusMachine.CanTransition(Status, target) || IsConsultOverride(Status, target)))
        {
            Status = target;
        }
    }

    private static bool IsConsultOverride(DocumentStatus from, DocumentStatus to) =>
        to is DocumentStatus.Accepted or DocumentStatus.Observed or DocumentStatus.Rejected or DocumentStatus.Sent
        && from is DocumentStatus.Draft or DocumentStatus.Generated or DocumentStatus.Signed or DocumentStatus.Sent or DocumentStatus.Failed;

    public void AddFile(GeneratedFile file)
    {
        var existing = _files.Find(f => f.Kind == file.Kind);
        if (existing is not null)
        {
            _files.Remove(existing);
        }

        _files.Add(file);
    }

    public GeneratedFile? GetFile(GeneratedFileKind kind) =>
        _files.LastOrDefault(file => file.Kind == kind);

    private void ApplyShippingGuide(ShippingGuideInfo info)
    {
        TransferReasonCode = info.TransferReason.Code;
        TransportModeCode = info.TransportMode.Code;
        TransferStartDate = info.TransferStartDate;
        GrossWeightKg = info.GrossWeightKg;
        PackageCount = info.PackageCount;
        OriginAddressLine = info.Origin.Line;
        OriginUbigeo = info.Origin.Ubigeo;
        OriginDepartment = info.Origin.Department;
        OriginProvince = info.Origin.Province;
        OriginDistrict = info.Origin.District;
        DestinationAddressLine = info.Destination.Line;
        DestinationUbigeo = info.Destination.Ubigeo;
        DestinationDepartment = info.Destination.Department;
        DestinationProvince = info.Destination.Province;
        DestinationDistrict = info.Destination.District;
        CarrierRuc = info.CarrierRuc;
        CarrierName = info.CarrierName;
        VehiclePlate = info.VehiclePlate;
        DriverLicense = info.DriverLicense;
        DriverDocumentType = info.DriverDocumentType?.Code;
        DriverDocumentNumber = info.DriverDocumentNumber;
    }

    private void RecalculateTotals(decimal globalDiscount, decimal globalCharge)
    {
        var totals = TaxCalculator.CalculateDocument(
            _items.Select(item => new DocumentLineInput(item.Quantity, item.UnitValue, item.Discount, item.Affectation)),
            Currency,
            globalDiscount,
            globalCharge);

        TaxableAmount = totals.TaxableAmount;
        ExemptAmount = totals.ExemptAmount;
        UnaffectedAmount = totals.UnaffectedAmount;
        FreeAmount = totals.FreeAmount;
        ExportAmount = totals.ExportAmount;
        IgvAmount = totals.IgvAmount;
        LineExtensionAmount = totals.LineExtensionAmount;
        TaxInclusiveAmount = totals.TaxInclusiveAmount;
        PayableAmount = totals.PayableAmount;
        DiscountAmount = totals.DiscountAmount;
        ChargeAmount = totals.ChargeAmount;
        AmountInWords = Services.AmountInWords.ForCurrency(PayableAmount, Currency);
    }

    private static void ValidateIssue(
        DocumentType documentType,
        string series,
        IdentityDocument recipientIdentity,
        RelatedDocument? relatedDocument,
        ShippingGuideInfo? shippingGuide,
        IReadOnlyList<DocumentItemDraft> items)
    {
        if (items.Count == 0)
        {
            throw new BusinessRuleException("DOCUMENT", "At least one item is required.");
        }

        var seriesCode = new DocumentSeriesCode(series);
        if (documentType.IsNote)
        {
            if (relatedDocument is null)
            {
                throw new BusinessRuleException("DOCUMENT", "Credit and debit notes require a related document.");
            }

            seriesCode.EnsureCompatibleWith(documentType, relatedDocument.DocumentType);
        }
        else
        {
            seriesCode.EnsureCompatibleWith(documentType);
        }

        if (documentType == DocumentType.Invoice && !recipientIdentity.Type.IsRuc)
        {
            throw new BusinessRuleException("DOCUMENT", "A factura requires a recipient identified by RUC.");
        }

        if (documentType.IsShippingGuide && shippingGuide is null)
        {
            throw new BusinessRuleException("DOCUMENT", "A shipping guide requires transfer data.");
        }

        if (!documentType.IsShippingGuide && shippingGuide is not null)
        {
            throw new BusinessRuleException("DOCUMENT", "Transfer data is only valid for shipping guides.");
        }

        if (string.IsNullOrWhiteSpace(recipientIdentity.Number) || recipientIdentity.Number.Length == 0)
        {
            throw new BusinessRuleException("DOCUMENT", "Recipient identity is required.");
        }
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record DocumentItemDraft(
    string? Code,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitValue,
    decimal Discount,
    TaxAffectationCode Affectation);
