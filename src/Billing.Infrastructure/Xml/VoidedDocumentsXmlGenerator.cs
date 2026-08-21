using System.Globalization;
using System.Text;
using System.Xml;
using Billing.Application.Abstractions;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Xml;

public sealed class VoidedDocumentsXmlGenerator : IVoidedDocumentsXmlGenerator
{
    private static readonly XmlWriterSettings Settings = new()
    {
        Encoding = new UTF8Encoding(false),
        Indent = true,
        OmitXmlDeclaration = false
    };

    public byte[] Generate(ElectronicDocument document, string voidId, DateOnly issueDate, string reason)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, Settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("VoidedDocuments", "urn:oasis:names:specification:ubl:schema:xsd:VoidedDocuments-2");
            writer.WriteAttributeString("xmlns", "cac", null, UblNamespaces.Cac);
            writer.WriteAttributeString("xmlns", "cbc", null, UblNamespaces.Cbc);
            writer.WriteAttributeString("xmlns", "ds", null, UblNamespaces.Ds);
            writer.WriteAttributeString("xmlns", "ext", null, UblNamespaces.Ext);

            writer.WriteStartElement("ext", "UBLExtensions", UblNamespaces.Ext);
            writer.WriteStartElement("ext", "UBLExtension", UblNamespaces.Ext);
            writer.WriteStartElement("ext", "ExtensionContent", UblNamespaces.Ext);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            Write(writer, "cbc", "UBLVersionID", "2.0");
            Write(writer, "cbc", "CustomizationID", "1.0");
            Write(writer, "cbc", "ID", voidId);
            Write(writer, "cbc", "ReferenceDate", document.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Write(writer, "cbc", "IssueDate", issueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            writer.WriteStartElement("cac", "Signature", UblNamespaces.Cac);
            Write(writer, "cbc", "ID", "IDSignSP");
            writer.WriteStartElement("cac", "SignatoryParty", UblNamespaces.Cac);
            writer.WriteStartElement("cac", "PartyIdentification", UblNamespaces.Cac);
            Write(writer, "cbc", "ID", document.IssuerRuc);
            writer.WriteEndElement();
            writer.WriteStartElement("cac", "PartyName", UblNamespaces.Cac);
            WriteCdata(writer, "cbc", "Name", document.IssuerLegalName);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("cac", "DigitalSignatureAttachment", UblNamespaces.Cac);
            writer.WriteStartElement("cac", "ExternalReference", UblNamespaces.Cac);
            Write(writer, "cbc", "URI", "#SignatureSP");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("cac", "AccountingSupplierParty", UblNamespaces.Cac);
            writer.WriteStartElement("cbc", "CustomerAssignedAccountID", UblNamespaces.Cbc);
            writer.WriteAttributeString("schemeID", "6");
            writer.WriteString(document.IssuerRuc);
            writer.WriteEndElement();
            writer.WriteStartElement("cac", "Party", UblNamespaces.Cac);
            writer.WriteStartElement("cac", "PartyLegalEntity", UblNamespaces.Cac);
            WriteCdata(writer, "cbc", "RegistrationName", document.IssuerLegalName);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("cac", "VoidedDocumentsLine", UblNamespaces.Cac);
            Write(writer, "cbc", "LineID", "1");
            Write(writer, "cbc", "DocumentTypeCode", document.DocumentTypeCode);
            Write(writer, "cbc", "DocumentSerialID", document.Series);
            Write(writer, "cbc", "DocumentNumberID", document.Number.ToString(CultureInfo.InvariantCulture));
            WriteCdata(writer, "cbc", "VoidReasonDescription", reason);
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return stream.ToArray();
    }

    private static void Write(XmlWriter writer, string prefix, string name, string value)
    {
        writer.WriteStartElement(prefix, name, prefix == "cbc" ? UblNamespaces.Cbc : UblNamespaces.Cac);
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    private static void WriteCdata(XmlWriter writer, string prefix, string name, string value)
    {
        writer.WriteStartElement(prefix, name, UblNamespaces.Cbc);
        writer.WriteCData(value);
        writer.WriteEndElement();
    }
}
