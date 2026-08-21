using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Billing.Application.Abstractions;
using Billing.Application.Exceptions;

namespace Billing.Infrastructure.Signing;

public sealed class XmlDsigSigner(ICertificateProvider certificateProvider) : IXmlSigner
{
    public SignedXmlResult Sign(byte[] xml)
    {
        var certificate = certificateProvider.GetCertificate();
        var privateKey = certificate.GetRSAPrivateKey();
        if (privateKey is null)
        {
            throw new InternalApplicationException("The certificate does not contain an RSA private key.");
        }

        var document = new XmlDocument { PreserveWhitespace = true };
        using (var stream = new MemoryStream(xml))
        {
            document.Load(stream);
        }

        var signedXml = new SignedXml(document) { SigningKey = privateKey };
        var signedInfo = signedXml.SignedInfo ?? throw new InternalApplicationException("SignedInfo was not initialized.");
        signedInfo.CanonicalizationMethod = SignedXml.XmlDsigCanonicalizationUrl;
        signedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;

        var reference = new Reference { Uri = string.Empty, DigestMethod = SignedXml.XmlDsigSHA1Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        signedXml.AddReference(reference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;
        signedXml.ComputeSignature();

        var signatureNode = signedXml.GetXml();
        var idAttribute = signatureNode.OwnerDocument!.CreateAttribute("Id");
        idAttribute.Value = "SignatureSP";
        signatureNode.Attributes!.Append(idAttribute);

        var namespaceManager = new XmlNamespaceManager(document.NameTable);
        namespaceManager.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
        var content = document.SelectSingleNode("//ext:ExtensionContent", namespaceManager)
                      ?? throw new InternalApplicationException("The UBL document does not contain ext:ExtensionContent.");
        content.AppendChild(document.ImportNode(signatureNode, true));

        var digest = signatureNode.GetElementsByTagName("DigestValue", "http://www.w3.org/2000/09/xmldsig#")[0]?.InnerText
                     ?? throw new InternalApplicationException("DigestValue was not produced by the signer.");

        using var output = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = false
        };
        using (var writer = XmlWriter.Create(output, settings))
        {
            document.Save(writer);
        }

        return new SignedXmlResult(output.ToArray(), digest);
    }
}
