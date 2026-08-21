using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Billing.Application.Abstractions;
using Billing.Application.Exceptions;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Sunat;

public sealed class SunatDirectElectronicDocumentProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<SunatOptions> options,
    ICdrParser cdrParser,
    ILogger<SunatDirectElectronicDocumentProvider> logger) : IElectronicDocumentProvider
{
    public const string BillClientName = "SunatBill";
    public const string GreClientName = "SunatGre";

    public async Task<SubmissionResult> SubmitAsync(ElectronicDocument document, byte[] signedXml, CancellationToken cancellationToken)
    {
        if (document.Type.IsShippingGuide)
        {
            return await SubmitGreAsync(document, signedXml, cancellationToken);
        }

        return await SendBillAsync(document, signedXml, cancellationToken);
    }

    public async Task<SubmissionResult> GetStatusAsync(ElectronicDocument document, string? ticket, CancellationToken cancellationToken)
    {
        if (document.Type.IsShippingGuide)
        {
            if (string.IsNullOrWhiteSpace(ticket))
            {
                throw new SunatUnavailableException("A GRE ticket is required to consult status.");
            }

            return await GetGreStatusAsync(ticket, cancellationToken);
        }

        return await GetStatusCdrAsync(document, cancellationToken);
    }

    public async Task<SubmissionResult> SendSummaryAsync(string xmlFileName, byte[] signedXml, CancellationToken cancellationToken)
    {
        var zipName = Path.ChangeExtension(xmlFileName, ".zip") ?? xmlFileName + ".zip";
        var zip = ZipPacker.PackXml(xmlFileName, signedXml);
        var envelope = BuildSoapEnvelope("sendSummary", options.Value.SolUser, options.Value.SolPassword, zipName, zip);
        var xml = await PostSoapAsync(options.Value.BillServiceUrl, envelope, cancellationToken);
        return ParseSendSummaryResponse(xml);
    }

    public async Task<SubmissionResult> GetSummaryStatusAsync(string ticket, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var envelope = $"""
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://service.sunat.gob.pe" xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
              <soapenv:Header>
                <wsse:Security>
                  <wsse:UsernameToken>
                    <wsse:Username>{System.Security.SecurityElement.Escape(settings.SolUser)}</wsse:Username>
                    <wsse:Password>{System.Security.SecurityElement.Escape(settings.SolPassword)}</wsse:Password>
                  </wsse:UsernameToken>
                </wsse:Security>
              </soapenv:Header>
              <soapenv:Body>
                <ser:getStatus>
                  <ticket>{System.Security.SecurityElement.Escape(ticket)}</ticket>
                </ser:getStatus>
              </soapenv:Body>
            </soapenv:Envelope>
            """;
        var xml = await PostSoapAsync(settings.BillServiceUrl, envelope, cancellationToken);
        return ParseStatusResponse(xml);
    }

    private async Task<SubmissionResult> SendBillAsync(ElectronicDocument document, byte[] signedXml, CancellationToken cancellationToken)
    {
        var zipName = document.XmlFileName + ".zip";
        var zip = ZipPacker.PackXml(document.XmlFileName + ".xml", signedXml);
        var envelope = BuildSoapEnvelope("sendBill", options.Value.SolUser, options.Value.SolPassword, zipName, zip);
        var xml = await PostSoapAsync(options.Value.BillServiceUrl, envelope, cancellationToken);
        return ParseSendBillResponse(xml);
    }

    private async Task<SubmissionResult> GetStatusCdrAsync(ElectronicDocument document, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var envelope = $"""
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://service.sunat.gob.pe" xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
              <soapenv:Header>
                <wsse:Security>
                  <wsse:UsernameToken>
                    <wsse:Username>{System.Security.SecurityElement.Escape(settings.SolUser)}</wsse:Username>
                    <wsse:Password>{System.Security.SecurityElement.Escape(settings.SolPassword)}</wsse:Password>
                  </wsse:UsernameToken>
                </wsse:Security>
              </soapenv:Header>
              <soapenv:Body>
                <ser:getStatusCdr>
                  <rucComprobante>{document.IssuerRuc}</rucComprobante>
                  <tipoComprobante>{document.DocumentTypeCode}</tipoComprobante>
                  <serieComprobante>{document.Series}</serieComprobante>
                  <numeroComprobante>{document.Number}</numeroComprobante>
                </ser:getStatusCdr>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        var xml = await PostSoapAsync(settings.ConsultServiceUrl, envelope, cancellationToken);
        return ParseStatusResponse(xml);
    }

    private async Task<SubmissionResult> SubmitGreAsync(ElectronicDocument document, byte[] signedXml, CancellationToken cancellationToken)
    {
        var token = await GetGreTokenAsync(cancellationToken);
        var zipName = document.XmlFileName + ".zip";
        var zip = ZipPacker.PackXml(document.XmlFileName + ".xml", signedXml);
        var payload = new
        {
            archivo = new
            {
                nomArchivo = zipName,
                arcGreZip = Convert.ToBase64String(zip),
                hashZip = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(zip)).ToLowerInvariant()
            }
        };

        var client = httpClientFactory.CreateClient(GreClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.GreApiUrl.TrimEnd('/')}/contribuyente/gem/comprobantes/{document.XmlFileName}")
        {
            Content = JsonContent(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await SendAsync(client, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw MapHttpError(response.StatusCode, body);
        }

        var ticket = ExtractJsonValue(body, "numTicket");
        return new SubmissionResult(SunatStatus.InProcess, null, "GRE accepted for processing.", null, ticket, null, null);
    }

    private async Task<SubmissionResult> GetGreStatusAsync(string ticket, CancellationToken cancellationToken)
    {
        var token = await GetGreTokenAsync(cancellationToken);
        var client = httpClientFactory.CreateClient(GreClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.Value.GreApiUrl.TrimEnd('/')}/contribuyente/gem/comprobantes/envios/{ticket}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await SendAsync(client, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw MapHttpError(response.StatusCode, body);
        }

        var code = ExtractJsonValue(body, "codRespuesta") ?? "98";
        if (code == "98")
        {
            return new SubmissionResult(SunatStatus.InProcess, "98", "SUNAT is still processing the GRE.", null, ticket, null, null);
        }

        var cdrB64 = ExtractJsonValue(body, "arcCdr");
        byte[]? cdrZip = string.IsNullOrWhiteSpace(cdrB64) ? null : Convert.FromBase64String(cdrB64);
        if (cdrZip is null)
        {
            return new SubmissionResult(code == "0" ? SunatStatus.Accepted : SunatStatus.Rejected, code, body, null, ticket, null, null);
        }

        var parsed = cdrParser.Parse(cdrZip);
        return new SubmissionResult(parsed.Status, parsed.ResponseCode, parsed.Description, parsed.Notes, ticket, cdrZip, parsed.OriginalXml);
    }

    private async Task<string> GetGreTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.GreClientId) || string.IsNullOrWhiteSpace(settings.GreTokenUrl))
        {
            throw new SunatUnavailableException("GRE OAuth credentials are not configured.");
        }

        var client = httpClientFactory.CreateClient(GreClientName);
        var tokenUrl = settings.GreTokenUrl.Replace("{client_id}", settings.GreClientId, StringComparison.OrdinalIgnoreCase);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["scope"] = "https://api-cpe.sunat.gob.pe",
                ["client_id"] = settings.GreClientId,
                ["client_secret"] = settings.GreClientSecret,
                ["username"] = settings.SolUser,
                ["password"] = settings.SolPassword
            })
        };
        var response = await SendAsync(client, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw MapHttpError(response.StatusCode, "GRE authentication failed.");
        }

        return ExtractJsonValue(body, "access_token")
               ?? throw new SunatUnavailableException("SUNAT did not return a GRE access token.");
    }

    private async Task<string> PostSoapAsync(string url, string envelope, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(BillClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");
        var response = await SendAsync(client, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw MapSoapOrHttpError(response.StatusCode, body);
        }

        return body;
    }

    private SubmissionResult ParseSendBillResponse(string xml)
    {
        if (xml.Contains("Fault", StringComparison.OrdinalIgnoreCase) && xml.Contains("faultstring", StringComparison.OrdinalIgnoreCase))
        {
            throw MapSoapFault(xml);
        }

        var applicationResponse = ExtractBase64(xml, "applicationResponse") ?? ExtractBase64(xml, "content");
        if (applicationResponse is null)
        {
            throw new SunatUnavailableException("SUNAT did not return a CDR in the sendBill response.");
        }

        var zip = Convert.FromBase64String(applicationResponse);
        var parsed = cdrParser.Parse(zip);
        if (parsed.Status == SunatStatus.Rejected)
        {
            logger.LogWarning("SUNAT rejected the document. Code={Code} Description={Description}", parsed.ResponseCode, parsed.Description);
        }

        return new SubmissionResult(parsed.Status, parsed.ResponseCode, parsed.Description, parsed.Notes, null, zip, parsed.OriginalXml);
    }

    private SubmissionResult ParseSendSummaryResponse(string xml)
    {
        if (xml.Contains("Fault", StringComparison.OrdinalIgnoreCase) && xml.Contains("faultstring", StringComparison.OrdinalIgnoreCase))
        {
            throw MapSoapFault(xml);
        }

        var ticket = ExtractXmlLocalValue(xml, "ticket");
        if (!string.IsNullOrWhiteSpace(ticket))
        {
            return new SubmissionResult(SunatStatus.InProcess, "98", "Comunicación de baja enviada. Consulte el estado SUNAT.", null, ticket, null, null);
        }

        return ParseStatusResponse(xml);
    }

    private SubmissionResult ParseStatusResponse(string xml)
    {
        if (xml.Contains("Fault", StringComparison.OrdinalIgnoreCase))
        {
            throw MapSoapFault(xml);
        }

        var content = ExtractBase64(xml, "content");
        var statusCode = ExtractXmlLocalValue(xml, "statusCode");
        if (content is null)
        {
            var status = statusCode switch
            {
                "0" => SunatStatus.Accepted,
                "98" => SunatStatus.InProcess,
                "99" => SunatStatus.Rejected,
                _ => SunatStatus.CommunicationError
            };
            var description = status switch
            {
                SunatStatus.Accepted => "SUNAT aceptó la consulta o la comunicación de baja.",
                SunatStatus.InProcess => "SUNAT sigue procesando la solicitud.",
                SunatStatus.Rejected => "SUNAT rechazó la solicitud.",
                _ => "No CDR content was returned."
            };
            return new SubmissionResult(status, statusCode, description, null, null, null, null);
        }

        var zip = Convert.FromBase64String(content);
        var parsed = cdrParser.Parse(zip);
        return new SubmissionResult(parsed.Status, parsed.ResponseCode, parsed.Description, parsed.Notes, null, zip, parsed.OriginalXml);
    }

    private static string BuildSoapEnvelope(string operation, string user, string password, string fileName, byte[] zip) =>
        $"""
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://service.sunat.gob.pe" xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
          <soapenv:Header>
            <wsse:Security>
              <wsse:UsernameToken>
                <wsse:Username>{System.Security.SecurityElement.Escape(user)}</wsse:Username>
                <wsse:Password>{System.Security.SecurityElement.Escape(password)}</wsse:Password>
              </wsse:UsernameToken>
            </wsse:Security>
          </soapenv:Header>
          <soapenv:Body>
            <ser:{operation}>
              <fileName>{System.Security.SecurityElement.Escape(fileName)}</fileName>
              <contentFile>{Convert.ToBase64String(zip)}</contentFile>
            </ser:{operation}>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransientCommunicationException("The SUNAT request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new TransientCommunicationException("The SUNAT endpoint could not be reached.", ex);
        }
    }

    private static Exception MapSoapOrHttpError(System.Net.HttpStatusCode statusCode, string body)
    {
        if (body.Contains("Fault", StringComparison.OrdinalIgnoreCase))
        {
            return MapSoapFault(body);
        }

        return MapHttpError(statusCode, body);
    }

    private static Exception MapSoapFault(string xml)
    {
        var code = ExtractXmlLocalValue(xml, "faultcode") ?? ExtractXmlLocalValue(xml, "faultCode");
        var message = ExtractXmlLocalValue(xml, "faultstring") ?? ExtractXmlLocalValue(xml, "message") ?? "SUNAT SOAP fault.";
        var numeric = ExtractNumericCode(code + " " + message);
        if (numeric is >= 2000 and <= 3999 or >= 1000 and <= 1999)
        {
            return new SunatRejectionException(message, numeric.ToString(), null);
        }

        return new SunatUnavailableException(message);
    }

    private static Exception MapHttpError(System.Net.HttpStatusCode statusCode, string body)
    {
        if ((int)statusCode >= 500 || statusCode == System.Net.HttpStatusCode.RequestTimeout)
        {
            return new SunatUnavailableException($"SUNAT returned HTTP {(int)statusCode}.");
        }

        if ((int)statusCode >= 400)
        {
            return new SunatRejectionException($"SUNAT returned HTTP {(int)statusCode}.", ((int)statusCode).ToString(), null);
        }

        return new TransientCommunicationException(body);
    }

    private static StringContent JsonContent(object payload) =>
        new(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static string? ExtractBase64(string xml, string localName)
    {
        try
        {
            var document = XDocument.Parse(xml);
            return document.Descendants().FirstOrDefault(x => x.Name.LocalName == localName)?.Value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ExtractXmlLocalValue(string xml, string localName)
    {
        try
        {
            return XDocument.Parse(xml).Descendants().FirstOrDefault(x => x.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ExtractJsonValue(string json, string property)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(property, out var value))
            {
                return value.ToString();
            }

            foreach (var child in document.RootElement.EnumerateObject())
            {
                if (child.Value.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    child.Value.TryGetProperty(property, out var nested))
                {
                    return nested.ToString();
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private static int? ExtractNumericCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits.Length > 4 ? digits[^4..] : digits, out var value) ? value : null;
    }
}
