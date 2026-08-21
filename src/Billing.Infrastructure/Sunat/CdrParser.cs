using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Billing.Application.Abstractions;
using Billing.Application.Exceptions;
using Billing.Domain.Enums;

namespace Billing.Infrastructure.Sunat;

public sealed class CdrParser : ICdrParser
{
    public CdrParseResult Parse(byte[] zipOrXml)
    {
        var xml = ExtractXml(zipOrXml);
        var document = XDocument.Parse(Encoding.UTF8.GetString(xml));
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

        var response = document.Descendants(cac + "Response").FirstOrDefault()
                       ?? document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Response");
        var code = response?.Element(cbc + "ResponseCode")?.Value
                   ?? document.Descendants().FirstOrDefault(x => x.Name.LocalName == "ResponseCode")?.Value
                   ?? throw new InternalApplicationException("The CDR does not contain a ResponseCode.");
        var description = response?.Element(cbc + "Description")?.Value
                          ?? document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Description")?.Value
                          ?? string.Empty;
        var notes = string.Join(" | ", document.Descendants(cbc + "Note").Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)));
        var issueDateText = document.Descendants(cbc + "IssueDate").FirstOrDefault()?.Value;
        DateTimeOffset? issueDate = DateTime.TryParse(issueDateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? new DateTimeOffset(parsed, TimeSpan.FromHours(-5))
            : null;

        var numericCode = int.TryParse(code, out var n) ? n : -1;
        var status = numericCode switch
        {
            0 => string.IsNullOrWhiteSpace(notes) ? SunatStatus.Accepted : SunatStatus.AcceptedWithObservations,
            >= 4000 => SunatStatus.AcceptedWithObservations,
            >= 2000 and <= 3999 => SunatStatus.Rejected,
            _ => SunatStatus.Rejected
        };

        if (numericCode == 0 && !string.IsNullOrWhiteSpace(notes))
        {
            status = SunatStatus.AcceptedWithObservations;
        }

        return new CdrParseResult(code, description, string.IsNullOrWhiteSpace(notes) ? null : notes, issueDate, status, xml);
    }

    private static byte[] ExtractXml(byte[] zipOrXml)
    {
        if (zipOrXml.Length >= 2 && zipOrXml[0] == 0x50 && zipOrXml[1] == 0x4B)
        {
            using var zipStream = new MemoryStream(zipOrXml);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var entryStream = entry.Open();
                    using var output = new MemoryStream();
                    entryStream.CopyTo(output);
                    return output.ToArray();
                }

                if (entry.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var nested = entry.Open();
                    using var nestedCopy = new MemoryStream();
                    nested.CopyTo(nestedCopy);
                    return ExtractXml(nestedCopy.ToArray());
                }
            }

            throw new InternalApplicationException("The SUNAT ZIP response does not contain an XML CDR.");
        }

        return zipOrXml;
    }
}

internal static class ZipPacker
{
    public static byte[] PackXml(string fileName, byte[] xml)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(xml);
        }

        return stream.ToArray();
    }
}
