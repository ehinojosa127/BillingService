using System.Globalization;
using Billing.Application.Abstractions;
using Billing.Domain.Entities;
using QRCoder;

namespace Billing.Infrastructure.Qr;

public sealed class SunatQrCodeGenerator : IQrCodeGenerator
{
    public string BuildPayload(ElectronicDocument document)
    {
        return string.Join('|',
            document.IssuerRuc,
            document.DocumentTypeCode,
            document.Series,
            document.Number.ToString(CultureInfo.InvariantCulture),
            document.IgvAmount.ToString("0.00", CultureInfo.InvariantCulture),
            document.PayableAmount.ToString("0.00", CultureInfo.InvariantCulture),
            document.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            document.RecipientIdentityType,
            document.RecipientIdentityNumber,
            document.DigestValue ?? string.Empty);
    }

    public byte[] GeneratePng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(8);
    }
}
