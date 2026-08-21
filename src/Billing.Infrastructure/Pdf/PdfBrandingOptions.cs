namespace Billing.Infrastructure.Pdf;

public sealed class PdfBrandingOptions
{
    public const string SectionName = "PdfBranding";

    public string CompanyName { get; set; } = "Confecciones Erika";
    public string PrimaryColor { get; set; } = "#1F4E79";
    public string LogoPath { get; set; } = "Pdf/Assets/Branding/logo-transparent.png";

    public string? LogoDataUri
    {
        get
        {
            var path = ResolveLogoPath();
            if (path is null)
            {
                return null;
            }

            var contents = File.ReadAllBytes(path);
            var mime = path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? "image/svg+xml"
                : "image/png";

            return $"data:{mime};base64,{Convert.ToBase64String(contents)}";
        }
    }

    public bool HasLogo => ResolveLogoPath() is not null;

    public string? ResolveLogoPath()
    {
        foreach (var candidate in LogoCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerable<string> LogoCandidates()
    {
        if (Path.IsPathRooted(LogoPath) && File.Exists(LogoPath))
        {
            yield return LogoPath;
        }

        var assemblyDir = Path.GetDirectoryName(typeof(PdfBrandingOptions).Assembly.Location) ?? AppContext.BaseDirectory;
        yield return Path.Combine(assemblyDir, LogoPath);
        yield return Path.Combine(assemblyDir, "Pdf", "Assets", "Branding", "logo-transparent.png");
        yield return Path.Combine(assemblyDir, "Pdf", "Assets", "Branding", "logo.png");
        yield return Path.Combine(AppContext.BaseDirectory, "Pdf", "Assets", "Branding", "logo-transparent.png");
    }
}
