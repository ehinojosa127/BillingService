using System.Globalization;
using System.Text;
using System.Xml;
using Billing.Application.Abstractions;
using Billing.Domain.Catalogs;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Xml;

public sealed class UblXmlDocumentGenerator : IXmlDocumentGenerator
{
    private static readonly XmlWriterSettings Settings = new()
    {
        Encoding = new UTF8Encoding(false),
        Indent = true,
        OmitXmlDeclaration = false,
        Async = false
    };

    public byte[] Generate(ElectronicDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, Settings))
        {
            writer.WriteStartDocument();
            WriteRoot(writer, document);
            writer.WriteEndDocument();
        }

        return stream.ToArray();
    }

    private static void WriteRoot(XmlWriter writer, ElectronicDocument document)
    {
        var (root, ns) = document.Type.Code switch
        {
            "07" => ("CreditNote", UblNamespaces.CreditNote),
            "08" => ("DebitNote", UblNamespaces.DebitNote),
            "09" => ("DespatchAdvice", UblNamespaces.DespatchAdvice),
            _ => ("Invoice", UblNamespaces.Invoice)
        };

        writer.WriteStartElement(root, ns);
        writer.WriteAttributeString("xmlns", "cac", null, UblNamespaces.Cac);
        writer.WriteAttributeString("xmlns", "cbc", null, UblNamespaces.Cbc);
        writer.WriteAttributeString("xmlns", "ds", null, UblNamespaces.Ds);
        writer.WriteAttributeString("xmlns", "ext", null, UblNamespaces.Ext);

        WriteEmptySignatureExtension(writer);
        WriteElement(writer, "cbc", "UBLVersionID", "2.1");
        WriteElement(writer, "cbc", "CustomizationID", document.Type.IsShippingGuide ? "2.0" : "2.0");
        WriteElement(writer, "cbc", "ID", document.FullNumber);
        WriteElement(writer, "cbc", "IssueDate", document.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        WriteElement(writer, "cbc", "IssueTime", document.IssueTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture));

        if (document.Type.IsShippingGuide)
        {
            WriteElement(writer, "cbc", "DespatchAdviceTypeCode", "09");
            WriteDespatchBody(writer, document);
        }
        else if (document.Type == DocumentType.CreditNote)
        {
            WriteElement(writer, "cbc", "DocumentCurrencyCode", document.Currency);
            WriteNote(writer, document);
            WriteNoteBody(writer, document, isCredit: true);
        }
        else if (document.Type == DocumentType.DebitNote)
        {
            WriteElement(writer, "cbc", "DocumentCurrencyCode", document.Currency);
            WriteNote(writer, document);
            WriteNoteBody(writer, document, isCredit: false);
        }
        else
        {
            writer.WriteStartElement("cbc", "InvoiceTypeCode", UblNamespaces.Cbc);
            writer.WriteAttributeString("listID", document.OperationTypeCode);
            writer.WriteString(document.DocumentTypeCode);
            writer.WriteEndElement();
            WriteNote(writer, document);
            WriteElement(writer, "cbc", "DocumentCurrencyCode", document.Currency);
            WriteInvoiceBody(writer, document);
        }

        writer.WriteEndElement();
    }

    private static void WriteEmptySignatureExtension(XmlWriter writer)
    {
        writer.WriteStartElement("ext", "UBLExtensions", UblNamespaces.Ext);
        writer.WriteStartElement("ext", "UBLExtension", UblNamespaces.Ext);
        writer.WriteStartElement("ext", "ExtensionContent", UblNamespaces.Ext);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteNote(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cbc", "Note", UblNamespaces.Cbc);
        writer.WriteAttributeString("languageLocaleID", "1000");
        writer.WriteCData(document.AmountInWords);
        writer.WriteEndElement();
    }

    private static void WriteInvoiceBody(XmlWriter writer, ElectronicDocument document)
    {
        WriteUblSignature(writer, document);
        WriteSupplier(writer, document);
        WriteCustomer(writer, document);
        WritePaymentTerms(writer, document);
        WriteTaxTotal(writer, document);
        WriteLegalMonetaryTotal(writer, document);
        foreach (var item in document.Items.OrderBy(x => x.LineNumber))
        {
            WriteInvoiceLine(writer, document, item);
        }
    }

    private static void WriteNoteBody(XmlWriter writer, ElectronicDocument document, bool isCredit)
    {
        foreach (var reference in document.References)
        {
            writer.WriteStartElement("cac", "DiscrepancyResponse", UblNamespaces.Cac);
            WriteElement(writer, "cbc", "ReferenceID", reference.FullNumber);
            WriteElement(writer, "cbc", "ResponseCode", reference.ReasonCode);
            WriteCData(writer, "cbc", "Description", reference.ReasonDescription);
            writer.WriteEndElement();

            writer.WriteStartElement("cac", "BillingReference", UblNamespaces.Cac);
            writer.WriteStartElement("cac", "InvoiceDocumentReference", UblNamespaces.Cac);
            WriteElement(writer, "cbc", "ID", reference.FullNumber);
            WriteElement(writer, "cbc", "DocumentTypeCode", reference.RelatedDocumentTypeCode);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        WriteUblSignature(writer, document);
        WriteSupplier(writer, document);
        WriteCustomer(writer, document);
        WriteTaxTotal(writer, document);
        WriteLegalMonetaryTotal(writer, document);
        foreach (var item in document.Items.OrderBy(x => x.LineNumber))
        {
            WriteNoteLine(writer, document, item, isCredit);
        }
    }

    private static void WriteDespatchBody(XmlWriter writer, ElectronicDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.Observation))
        {
            WriteCData(writer, "cbc", "Note", document.Observation);
        }

        WriteUblSignature(writer, document);
        WriteDespatchSupplier(writer, document);
        WriteDespatchCustomer(writer, document);
        WriteShipment(writer, document);
        foreach (var item in document.Items.OrderBy(x => x.LineNumber))
        {
            writer.WriteStartElement("cac", "DespatchLine", UblNamespaces.Cac);
            WriteElement(writer, "cbc", "ID", item.LineNumber.ToString(CultureInfo.InvariantCulture));
            writer.WriteStartElement("cbc", "DeliveredQuantity", UblNamespaces.Cbc);
            writer.WriteAttributeString("unitCode", item.UnitCode);
            writer.WriteString(FormatQuantity(item.Quantity));
            writer.WriteEndElement();
            writer.WriteStartElement("cac", "OrderLineReference", UblNamespaces.Cac);
            WriteElement(writer, "cbc", "LineID", item.LineNumber.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            writer.WriteStartElement("cac", "Item", UblNamespaces.Cac);
            WriteCData(writer, "cbc", "Description", item.Description);
            if (!string.IsNullOrWhiteSpace(item.Code))
            {
                writer.WriteStartElement("cac", "SellersItemIdentification", UblNamespaces.Cac);
                WriteElement(writer, "cbc", "ID", item.Code);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }

    private static void WriteUblSignature(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "Signature", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", document.IssuerRuc);
        writer.WriteStartElement("cac", "SignatoryParty", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "PartyIdentification", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", document.IssuerRuc);
        writer.WriteEndElement();
        writer.WriteStartElement("cac", "PartyName", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "Name", document.IssuerLegalName);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cac", "DigitalSignatureAttachment", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "ExternalReference", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "URI", "#SignatureSP");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteSupplier(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "AccountingSupplierParty", UblNamespaces.Cac);
        WriteParty(writer, document.IssuerRuc, "6", document.IssuerTradeName, document.IssuerLegalName, document);
        writer.WriteEndElement();
    }

    private static void WriteCustomer(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "AccountingCustomerParty", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "Party", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "PartyIdentification", UblNamespaces.Cac);
        writer.WriteStartElement("cbc", "ID", UblNamespaces.Cbc);
        writer.WriteAttributeString("schemeID", document.RecipientIdentityType);
        writer.WriteString(document.RecipientIdentityNumber);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cac", "PartyLegalEntity", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "RegistrationName", document.RecipientName);
        if (!string.IsNullOrWhiteSpace(document.RecipientAddressLine))
        {
            writer.WriteStartElement("cac", "RegistrationAddress", UblNamespaces.Cac);
            writer.WriteStartElement("cac", "AddressLine", UblNamespaces.Cac);
            WriteCData(writer, "cbc", "Line", document.RecipientAddressLine);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteParty(XmlWriter writer, string ruc, string schemeId, string tradeName, string legalName, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "Party", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "PartyIdentification", UblNamespaces.Cac);
        writer.WriteStartElement("cbc", "ID", UblNamespaces.Cbc);
        writer.WriteAttributeString("schemeID", schemeId);
        writer.WriteString(ruc);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cac", "PartyName", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "Name", tradeName);
        writer.WriteEndElement();
        writer.WriteStartElement("cac", "PartyLegalEntity", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "RegistrationName", legalName);
        writer.WriteStartElement("cac", "RegistrationAddress", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", document.IssuerUbigeo);
        WriteElement(writer, "cbc", "AddressTypeCode", document.IssuerEstablishmentCode);
        if (!string.IsNullOrWhiteSpace(document.IssuerUrbanization))
        {
            WriteElement(writer, "cbc", "CitySubdivisionName", document.IssuerUrbanization);
        }

        WriteElement(writer, "cbc", "CityName", document.IssuerProvince);
        WriteElement(writer, "cbc", "CountrySubentity", document.IssuerDepartment);
        WriteElement(writer, "cbc", "District", document.IssuerDistrict);
        writer.WriteStartElement("cac", "AddressLine", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "Line", document.IssuerAddressLine);
        writer.WriteEndElement();
        writer.WriteStartElement("cac", "Country", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "IdentificationCode", document.IssuerCountryCode);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        if (!string.IsNullOrWhiteSpace(document.IssuerPhone) || !string.IsNullOrWhiteSpace(document.IssuerEmail))
        {
            writer.WriteStartElement("cac", "Contact", UblNamespaces.Cac);
            if (!string.IsNullOrWhiteSpace(document.IssuerPhone))
            {
                WriteElement(writer, "cbc", "Telephone", document.IssuerPhone);
            }

            if (!string.IsNullOrWhiteSpace(document.IssuerEmail))
            {
                WriteElement(writer, "cbc", "ElectronicMail", document.IssuerEmail);
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WritePaymentTerms(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "PaymentTerms", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", "FormaPago");
        WriteElement(writer, "cbc", "PaymentMeansID", document.PaymentForm == Domain.Enums.PaymentForm.Credit ? "Credito" : "Contado");
        writer.WriteEndElement();
    }

    private static void WriteTaxTotal(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "TaxTotal", UblNamespaces.Cac);
        WriteAmount(writer, "cbc", "TaxAmount", document.IgvAmount, document.Currency);
        if (document.TaxableAmount > 0 || document.IgvAmount > 0)
        {
            WriteTaxSubtotal(writer, document.TaxableAmount, document.IgvAmount, document.Currency, "1000", "IGV", "VAT");
        }

        if (document.ExemptAmount > 0)
        {
            WriteTaxSubtotal(writer, document.ExemptAmount, 0, document.Currency, "9997", "EXO", "VAT");
        }

        if (document.UnaffectedAmount > 0)
        {
            WriteTaxSubtotal(writer, document.UnaffectedAmount, 0, document.Currency, "9998", "INA", "FRE");
        }

        if (document.FreeAmount > 0)
        {
            WriteTaxSubtotal(writer, document.FreeAmount, 0, document.Currency, "9996", "GRA", "FRE");
        }

        if (document.ExportAmount > 0)
        {
            WriteTaxSubtotal(writer, document.ExportAmount, 0, document.Currency, "9995", "EXP", "FRE");
        }

        writer.WriteEndElement();
    }

    private static void WriteTaxSubtotal(XmlWriter writer, decimal taxable, decimal tax, string currency, string schemeId, string name, string typeCode)
    {
        writer.WriteStartElement("cac", "TaxSubtotal", UblNamespaces.Cac);
        WriteAmount(writer, "cbc", "TaxableAmount", taxable, currency);
        WriteAmount(writer, "cbc", "TaxAmount", tax, currency);
        writer.WriteStartElement("cac", "TaxCategory", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "TaxScheme", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", schemeId);
        WriteElement(writer, "cbc", "Name", name);
        WriteElement(writer, "cbc", "TaxTypeCode", typeCode);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteLegalMonetaryTotal(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "LegalMonetaryTotal", UblNamespaces.Cac);
        WriteAmount(writer, "cbc", "LineExtensionAmount", document.LineExtensionAmount, document.Currency);
        WriteAmount(writer, "cbc", "TaxInclusiveAmount", document.TaxInclusiveAmount, document.Currency);
        if (document.DiscountAmount > 0)
        {
            WriteAmount(writer, "cbc", "AllowanceTotalAmount", document.DiscountAmount, document.Currency);
        }

        if (document.ChargeAmount > 0)
        {
            WriteAmount(writer, "cbc", "ChargeTotalAmount", document.ChargeAmount, document.Currency);
        }

        WriteAmount(writer, "cbc", "PayableAmount", document.PayableAmount, document.Currency);
        writer.WriteEndElement();
    }

    private static void WriteInvoiceLine(XmlWriter writer, ElectronicDocument document, DocumentItem item)
    {
        writer.WriteStartElement("cac", "InvoiceLine", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", item.LineNumber.ToString(CultureInfo.InvariantCulture));
        writer.WriteStartElement("cbc", "InvoicedQuantity", UblNamespaces.Cbc);
        writer.WriteAttributeString("unitCode", item.UnitCode);
        writer.WriteString(FormatQuantity(item.Quantity));
        writer.WriteEndElement();
        WriteAmount(writer, "cbc", "LineExtensionAmount", item.TaxableAmount, document.Currency);
        WritePricingReference(writer, document, item);
        WriteLineTax(writer, document, item);
        WriteItem(writer, item);
        writer.WriteStartElement("cac", "Price", UblNamespaces.Cac);
        WriteAmount(writer, "cbc", "PriceAmount", item.UnitValue, document.Currency);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteNoteLine(XmlWriter writer, ElectronicDocument document, DocumentItem item, bool isCredit)
    {
        var lineName = isCredit ? "CreditNoteLine" : "DebitNoteLine";
        var qtyName = isCredit ? "CreditedQuantity" : "DebitedQuantity";
        writer.WriteStartElement("cac", lineName, UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", item.LineNumber.ToString(CultureInfo.InvariantCulture));
        writer.WriteStartElement("cbc", qtyName, UblNamespaces.Cbc);
        writer.WriteAttributeString("unitCode", item.UnitCode);
        writer.WriteString(FormatQuantity(item.Quantity));
        writer.WriteEndElement();
        WriteAmount(writer, "cbc", "LineExtensionAmount", item.TaxableAmount, document.Currency);
        WritePricingReference(writer, document, item);
        WriteLineTax(writer, document, item);
        WriteItem(writer, item);
        writer.WriteStartElement("cac", "Price", UblNamespaces.Cac);
        WriteAmount(writer, "cbc", "PriceAmount", item.UnitValue, document.Currency);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WritePricingReference(XmlWriter writer, ElectronicDocument document, DocumentItem item)
    {
        writer.WriteStartElement("cac", "PricingReference", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "AlternativeConditionPrice", UblNamespaces.Cac);
        WriteAmount(writer, "cbc", "PriceAmount", item.Affectation.IsFree ? item.UnitValue : item.UnitPrice, document.Currency);
        WriteElement(writer, "cbc", "PriceTypeCode", item.Affectation.IsFree ? "02" : "01");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteLineTax(XmlWriter writer, ElectronicDocument document, DocumentItem item)
    {
        var affectation = item.Affectation;
        writer.WriteStartElement("cac", "TaxTotal", UblNamespaces.Cac);
        WriteAmount(writer, "cbc", "TaxAmount", item.IgvAmount, document.Currency);
        writer.WriteStartElement("cac", "TaxSubtotal", UblNamespaces.Cac);
        WriteAmount(writer, "cbc", "TaxableAmount", item.TaxableAmount, document.Currency);
        WriteAmount(writer, "cbc", "TaxAmount", item.IgvAmount, document.Currency);
        writer.WriteStartElement("cac", "TaxCategory", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "Percent", affectation.IgvRate == 0 ? "0" : "18");
        WriteElement(writer, "cbc", "TaxExemptionReasonCode", affectation.Code);
        writer.WriteStartElement("cac", "TaxScheme", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", affectation.TaxSchemeId);
        WriteElement(writer, "cbc", "Name", affectation.TaxSchemeName);
        WriteElement(writer, "cbc", "TaxTypeCode", affectation.TaxTypeCode);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteItem(XmlWriter writer, DocumentItem item)
    {
        writer.WriteStartElement("cac", "Item", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "Description", item.Description);
        if (!string.IsNullOrWhiteSpace(item.Code))
        {
            writer.WriteStartElement("cac", "SellersItemIdentification", UblNamespaces.Cac);
            WriteElement(writer, "cbc", "ID", item.Code);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteDespatchSupplier(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "DespatchSupplierParty", UblNamespaces.Cac);
        WriteParty(writer, document.IssuerRuc, "6", document.IssuerTradeName, document.IssuerLegalName, document);
        writer.WriteEndElement();
    }

    private static void WriteDespatchCustomer(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "DeliveryCustomerParty", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "Party", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "PartyIdentification", UblNamespaces.Cac);
        writer.WriteStartElement("cbc", "ID", UblNamespaces.Cbc);
        writer.WriteAttributeString("schemeID", document.RecipientIdentityType);
        writer.WriteString(document.RecipientIdentityNumber);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cac", "PartyLegalEntity", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "RegistrationName", document.RecipientName);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteShipment(XmlWriter writer, ElectronicDocument document)
    {
        writer.WriteStartElement("cac", "Shipment", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", "1");
        WriteElement(writer, "cbc", "HandlingCode", document.TransferReasonCode ?? "01");
        WriteElement(writer, "cbc", "Information", "Traslado de bienes");
        writer.WriteStartElement("cbc", "GrossWeightMeasure", UblNamespaces.Cbc);
        writer.WriteAttributeString("unitCode", "KGM");
        writer.WriteString((document.GrossWeightKg ?? 0).ToString("0.###", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        WriteElement(writer, "cbc", "SplitConsignmentIndicator", "false");
        writer.WriteStartElement("cac", "ShipmentStage", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "TransportModeCode", document.TransportModeCode ?? "02");
        writer.WriteStartElement("cac", "TransitPeriod", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "StartDate", (document.TransferStartDate ?? document.IssueDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        if (!string.IsNullOrWhiteSpace(document.CarrierRuc))
        {
            writer.WriteStartElement("cac", "CarrierParty", UblNamespaces.Cac);
            writer.WriteStartElement("cac", "PartyIdentification", UblNamespaces.Cac);
            writer.WriteStartElement("cbc", "ID", UblNamespaces.Cbc);
            writer.WriteAttributeString("schemeID", "6");
            writer.WriteString(document.CarrierRuc);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("cac", "PartyLegalEntity", UblNamespaces.Cac);
            WriteCData(writer, "cbc", "RegistrationName", document.CarrierName ?? document.CarrierRuc);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        if (!string.IsNullOrWhiteSpace(document.DriverDocumentNumber))
        {
            writer.WriteStartElement("cac", "DriverPerson", UblNamespaces.Cac);
            writer.WriteStartElement("cbc", "ID", UblNamespaces.Cbc);
            writer.WriteAttributeString("schemeID", document.DriverDocumentType ?? "1");
            writer.WriteString(document.DriverDocumentNumber);
            writer.WriteEndElement();
            if (!string.IsNullOrWhiteSpace(document.DriverLicense))
            {
                writer.WriteStartElement("cac", "IdentityDocumentReference", UblNamespaces.Cac);
                WriteElement(writer, "cbc", "ID", document.DriverLicense);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteStartElement("cac", "Delivery", UblNamespaces.Cac);
        writer.WriteStartElement("cac", "DeliveryAddress", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", document.DestinationUbigeo ?? string.Empty);
        writer.WriteStartElement("cac", "AddressLine", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "Line", document.DestinationAddressLine ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cac", "OriginAddress", UblNamespaces.Cac);
        WriteElement(writer, "cbc", "ID", document.OriginUbigeo ?? string.Empty);
        writer.WriteStartElement("cac", "AddressLine", UblNamespaces.Cac);
        WriteCData(writer, "cbc", "Line", document.OriginAddressLine ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        if (!string.IsNullOrWhiteSpace(document.VehiclePlate))
        {
            writer.WriteStartElement("cac", "TransportHandlingUnit", UblNamespaces.Cac);
            writer.WriteStartElement("cac", "TransportEquipment", UblNamespaces.Cac);
            WriteElement(writer, "cbc", "ID", document.VehiclePlate);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteElement(XmlWriter writer, string prefix, string localName, string value)
    {
        var ns = prefix switch
        {
            "cbc" => UblNamespaces.Cbc,
            "cac" => UblNamespaces.Cac,
            "ext" => UblNamespaces.Ext,
            _ => UblNamespaces.Cbc
        };
        writer.WriteStartElement(prefix, localName, ns);
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    private static void WriteCData(XmlWriter writer, string prefix, string localName, string value)
    {
        writer.WriteStartElement(prefix, localName, prefix == "cbc" ? UblNamespaces.Cbc : UblNamespaces.Cac);
        writer.WriteCData(value);
        writer.WriteEndElement();
    }

    private static void WriteAmount(XmlWriter writer, string prefix, string localName, decimal amount, string currency)
    {
        writer.WriteStartElement(prefix, localName, UblNamespaces.Cbc);
        writer.WriteAttributeString("currencyID", currency);
        writer.WriteString(amount.ToString("0.00", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static string FormatQuantity(decimal quantity)
    {
        return quantity == decimal.Truncate(quantity)
            ? decimal.Truncate(quantity).ToString(CultureInfo.InvariantCulture)
            : quantity.ToString("0.######", CultureInfo.InvariantCulture);
    }
}

internal static class UblNamespaces
{
    public const string Invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string CreditNote = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    public const string DebitNote = "urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2";
    public const string DespatchAdvice = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";
    public const string Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public const string Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public const string Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    public const string Ds = "http://www.w3.org/2000/09/xmldsig#";
}
