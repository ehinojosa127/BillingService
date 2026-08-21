using System.Text;
using System.Xml.Linq;
using Billing.Infrastructure.Xml;
using Billing.Tests.Domain;

namespace Billing.Tests.Infrastructure;

public sealed class XmlDocumentGeneratorTests
{
    private readonly UblXmlDocumentGenerator _generator = new();

    [Fact]
    public void Invoice_xml_has_ubl_namespaces_and_required_nodes()
    {
        var xml = Encoding.UTF8.GetString(_generator.Generate(DocumentFactory.Invoice()));
        var document = XDocument.Parse(xml);
        Assert.Equal("Invoice", document.Root!.Name.LocalName);
        Assert.Contains("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", document.Root.Name.NamespaceName);
        Assert.Contains("<cbc:UBLVersionID>2.1</cbc:UBLVersionID>", xml);
        Assert.Contains("<cbc:CustomizationID>2.0</cbc:CustomizationID>", xml);
        Assert.Contains("<cbc:ID>F001-00001</cbc:ID>", xml);
        Assert.Contains("listID=\"0101\"", xml);
        Assert.Contains(">01</cbc:InvoiceTypeCode>", xml);
        Assert.Contains("schemeID=\"6\">20100070970</cbc:ID>", xml);
        Assert.Contains("schemeID=\"6\">20000000001</cbc:ID>", xml);
        Assert.Contains("<cbc:TaxExemptionReasonCode>10</cbc:TaxExemptionReasonCode>", xml);
        Assert.Contains("currencyID=\"PEN\">18.00</cbc:TaxAmount>", xml);
        Assert.Contains("currencyID=\"PEN\">118.00</cbc:PayableAmount>", xml);
        Assert.Contains("ext:ExtensionContent", xml);
        Assert.False(xml.StartsWith('\uFEFF'));
    }

    [Fact]
    public void Receipt_xml_uses_boleta_type_code()
    {
        var xml = Encoding.UTF8.GetString(_generator.Generate(DocumentFactory.Receipt()));
        Assert.Contains(">03</cbc:InvoiceTypeCode>", xml);
        Assert.Contains("<cbc:ID>B001-00001</cbc:ID>", xml);
        Assert.Contains("schemeID=\"1\">12345678</cbc:ID>", xml);
    }

    [Fact]
    public void Credit_note_xml_includes_billing_reference()
    {
        var xml = Encoding.UTF8.GetString(_generator.Generate(DocumentFactory.CreditNote()));
        Assert.Contains("CreditNote", xml);
        Assert.Contains("<cbc:ResponseCode>01</cbc:ResponseCode>", xml);
        Assert.Contains("<cbc:DocumentTypeCode>01</cbc:DocumentTypeCode>", xml);
        Assert.Contains("CreditNoteLine", xml);
    }

    [Fact]
    public void Debit_note_xml_includes_debit_line()
    {
        var xml = Encoding.UTF8.GetString(_generator.Generate(DocumentFactory.DebitNote()));
        Assert.Contains("DebitNote", xml);
        Assert.Contains("DebitNoteLine", xml);
    }
}
