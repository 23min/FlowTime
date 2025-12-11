using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace FlowTime.UI.Configuration;

public sealed class DiagnosticsOptions
{
    public HoverDiagnosticsOptions Hover { get; set; } = new();
}

public sealed class HoverDiagnosticsOptions
{
    /// <summary>
    /// When true the diagnostics HUD is rendered by default (can still be overridden via query string).
    /// </summary>
    public bool EnableOverlay { get; set; }

    /// <summary>
    /// When true the client will periodically upload hover diagnostics to the configured endpoint.
    /// </summary>
    public bool AutoUploadEnabled { get; set; }

    /// <summary>
    /// Interval (seconds) between automatic uploads.
    /// </summary>
    public int UploadIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Optional relative or absolute path for diagnostics uploads (e.g. "v1/diagnostics/hover").
    /// </summary>
    public string? UploadPath { get; set; }
}

public sealed class BuildDiagnostics
{
    public string Version { get; }
    public string Hash { get; }

    private BuildDiagnostics(string version, string hash)
    {
        Version = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version;
        Hash = string.IsNullOrWhiteSpace(hash) ? "dev" : hash;
    }

    public static BuildDiagnostics Create(Assembly assembly)
    {
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string resolvedVersion = informationalVersion?.Split('+')[0]
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        var resolvedHash = ComputeHash(informationalVersion ?? resolvedVersion ?? "dev");
        return new BuildDiagnostics(resolvedVersion ?? "0.0.0", resolvedHash);
    }

    private static string ComputeHash(string seed)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(seed);
        var hash = sha.ComputeHash(bytes);
        // Lowercase for readability, 12 chars is enough to differentiate builds.
        return Convert.ToHexString(hash).Substring(0, 12).ToLowerInvariant();
    }
}
