using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Billing.Infrastructure.Pdf;

public sealed class ChromiumHtmlToPdfRenderer : IAsyncDisposable
{
    private readonly ILogger<ChromiumHtmlToPdfRenderer> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IBrowser? _browser;
    private string? _resolvedExecutablePath;

    public ChromiumHtmlToPdfRenderer(ILogger<ChromiumHtmlToPdfRenderer> logger)
    {
        _logger = logger;
    }

    public Task WarmupAsync(CancellationToken cancellationToken = default) =>
        EnsureBrowserAsync(cancellationToken);

    public async Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken)
    {
        await EnsureBrowserAsync(cancellationToken);
        await using var page = await _browser!.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions
        {
            Width = 794,
            Height = 1123,
            DeviceScaleFactor = 2
        });
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Load],
            Timeout = 60000
        });
        await page.EmulateMediaTypeAsync(MediaType.Print);
        return await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = false,
            DisplayHeaderFooter = false,
            MarginOptions = new MarginOptions
            {
                Top = "16mm",
                Bottom = "16mm",
                Left = "16mm",
                Right = "16mm"
            }
        });
    }

    private async Task EnsureBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true })
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_browser is { IsConnected: true })
            {
                return;
            }

            var executablePath = await ResolveExecutablePathAsync(cancellationToken);
            _logger.LogInformation("Launching Chromium for PDF rendering from {ExecutablePath}", executablePath);

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = executablePath,
                Args =
                [
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                    "--font-render-hinting=none",
                    "--disable-software-rasterizer"
                ]
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> ResolveExecutablePathAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_resolvedExecutablePath) && File.Exists(_resolvedExecutablePath))
        {
            return _resolvedExecutablePath;
        }

        foreach (var candidate in SystemExecutableCandidates())
        {
            if (File.Exists(candidate))
            {
                _resolvedExecutablePath = candidate;
                _logger.LogInformation("Using system Chromium at {ExecutablePath}", candidate);
                return candidate;
            }
        }

        var platform = DetectPlatform();
        var cachePath = Path.Combine(AppContext.BaseDirectory, "Chrome");
        Directory.CreateDirectory(cachePath);

        _logger.LogInformation(
            "Downloading Chromium for PDF rendering (platform={Platform}, cache={CachePath}).",
            platform,
            cachePath);

        var fetcher = new BrowserFetcher(new BrowserFetcherOptions
        {
            Platform = platform,
            Path = cachePath
        });

        cancellationToken.ThrowIfCancellationRequested();
        var installed = await fetcher.DownloadAsync();
        _resolvedExecutablePath = installed.GetExecutablePath();
        return _resolvedExecutablePath;
    }

    private static IEnumerable<string> SystemExecutableCandidates()
    {
        var fromEnv = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH")
            ?? Environment.GetEnvironmentVariable("CHROME_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            yield return fromEnv;
        }

        yield return "/usr/bin/chromium";
        yield return "/usr/bin/chromium-browser";
        yield return "/usr/bin/google-chrome";
        yield return "/usr/bin/google-chrome-stable";
        yield return "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
    }

    private static Platform DetectPlatform()
    {
        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? Platform.MacOSArm64
                : Platform.MacOS;
        }

        if (OperatingSystem.IsWindows())
        {
            return RuntimeInformation.OSArchitecture is Architecture.X86 or Architecture.Arm
                ? Platform.Win32
                : Platform.Win64;
        }

        // Linux (incl. contenedores Docker en Apple Silicon → aarch64).
        return RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? Platform.LinuxArm64
            : Platform.Linux;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
            await _browser.DisposeAsync();
        }

        _gate.Dispose();
    }
}
