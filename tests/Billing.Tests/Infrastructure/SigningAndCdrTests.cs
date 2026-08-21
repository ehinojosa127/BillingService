using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Billing.Application.Abstractions;
using Billing.Infrastructure.Signing;
using Billing.Infrastructure.Sunat;
using Billing.Infrastructure.Xml;
using Billing.Tests.Domain;

namespace Billing.Tests.Infrastructure;

public sealed class XmlSignerTests
{
    [Fact]
    public void Sign_injects_signature_and_digest_without_bom()
    {
        var xml = new UblXmlDocumentGenerator().Generate(DocumentFactory.Invoice());
        var signer = new XmlDsigSigner(new StaticCertificateProvider());
        AppContext.SetSwitch("Switch.System.Security.Cryptography.Xml.UseInsecureHashAlgorithms", true);
        var result = signer.Sign(xml);
        var signed = Encoding.UTF8.GetString(result.Xml);
        Assert.False(string.IsNullOrWhiteSpace(result.DigestValue));
        Assert.Contains("SignatureSP", signed);
        Assert.Contains("DigestValue", signed);
        Assert.False(signed.StartsWith('\uFEFF'));
    }
}

public sealed class CdrParserTests
{
    [Fact]
    public void Accepted_cdr_with_code_zero_is_accepted()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ApplicationResponse xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
              <cbc:IssueDate>2026-08-18</cbc:IssueDate>
              <cac:DocumentResponse>
                <cac:Response>
                  <cbc:ReferenceID>F001-1</cbc:ReferenceID>
                  <cbc:ResponseCode>0</cbc:ResponseCode>
                  <cbc:Description>La Factura numero F001-1, ha sido aceptada</cbc:Description>
                </cac:Response>
              </cac:DocumentResponse>
            </ApplicationResponse>
            """;
        var result = new CdrParser().Parse(Encoding.UTF8.GetBytes(xml));
        Assert.Equal("0", result.ResponseCode);
        Assert.Equal(Billing.Domain.Enums.SunatStatus.Accepted, result.Status);
    }

    [Fact]
    public void Code_4000_plus_is_accepted_with_observations()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ar:ApplicationResponse xmlns:ar="urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
              <cac:DocumentResponse>
                <cac:Response>
                  <cbc:ResponseCode>0</cbc:ResponseCode>
                  <cbc:Description>Aceptada</cbc:Description>
                  <cbc:Note>4000 - El comprobante tiene observaciones</cbc:Note>
                </cac:Response>
              </cac:DocumentResponse>
            </ar:ApplicationResponse>
            """;
        var result = new CdrParser().Parse(Encoding.UTF8.GetBytes(xml));
        Assert.Equal(Billing.Domain.Enums.SunatStatus.AcceptedWithObservations, result.Status);
    }

    [Fact]
    public void Code_2000_range_is_rejected()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ApplicationResponse xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
              <cac:DocumentResponse>
                <cac:Response>
                  <cbc:ResponseCode>2325</cbc:ResponseCode>
                  <cbc:Description>El documento no cumple con las validaciones</cbc:Description>
                </cac:Response>
              </cac:DocumentResponse>
            </ApplicationResponse>
            """;
        var result = new CdrParser().Parse(Encoding.UTF8.GetBytes(xml));
        Assert.Equal(Billing.Domain.Enums.SunatStatus.Rejected, result.Status);
        Assert.Equal("2325", result.ResponseCode);
    }
}

file sealed class StaticCertificateProvider : ICertificateProvider
{
    public X509Certificate2 GetCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=BillingService-Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx, "test"), "test", X509KeyStorageFlags.Exportable);
    }
}
