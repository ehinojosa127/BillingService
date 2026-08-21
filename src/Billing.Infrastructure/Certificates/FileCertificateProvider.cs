using System.Security.Cryptography.X509Certificates;
using Billing.Application.Abstractions;
using Billing.Application.Exceptions;
using Billing.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Certificates;

public sealed class FileCertificateProvider(IOptions<SunatOptions> options) : ICertificateProvider
{
    public X509Certificate2 GetCertificate()
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.CertificatePath))
        {
            throw new InternalApplicationException("SUNAT_CERTIFICATE_PATH is not configured.");
        }

        if (!File.Exists(settings.CertificatePath))
        {
            throw new InternalApplicationException("The configured digital certificate file was not found.");
        }

        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(
                settings.CertificatePath,
                settings.CertificatePassword,
                X509KeyStorageFlags.Exportable);
        }
        catch (Exception ex)
        {
            throw new InternalApplicationException("The digital certificate could not be loaded.", ex);
        }
    }
}
