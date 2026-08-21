namespace Billing.Infrastructure.Configuration;

/// <summary>
/// Carga un archivo <c>.env</c> hacia variables de entorno, sin sobrescribir valores ya definidos.
/// En Development el flujo es: <c>.env.example</c> (referencia) → <c>.env</c> → Environment → Options.
/// En Production deben usarse environment variables reales.
/// </summary>
public static class EnvFileLoader
{
    public static void LoadDefaultLocations()
    {
        var current = Directory.GetCurrentDirectory();
        var candidates = new List<string>
        {
            current,
            Path.GetFullPath(Path.Combine(current, "..")),
            Path.GetFullPath(Path.Combine(current, "..", "..")),
            Path.GetFullPath(Path.Combine(current, "..", "..", "..")),
            AppContext.BaseDirectory
        };

        var walking = new DirectoryInfo(current);
        for (var i = 0; i < 8 && walking is not null; i++)
        {
            candidates.Add(walking.FullName);
            walking = walking.Parent;
        }

        foreach (var directory in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            LoadFile(Path.Combine(directory, ".env"));
        }
    }

    public static void LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(key) && Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
