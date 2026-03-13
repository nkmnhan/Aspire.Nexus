namespace Aspire.Nexus.Configuration;

/// <summary>
/// Certificate configuration. Supports two formats:
/// <list type="bullet">
///   <item><b>PEM</b>: Set <see cref="Path"/> (certificate) + <see cref="KeyPath"/> (private key)</item>
///   <item><b>PFX</b>: Set <see cref="Path"/> (certificate) + <see cref="Password"/></item>
/// </list>
/// </summary>
public sealed class CertificateConfig
{
    /// <summary>Path to the certificate file (.pem or .pfx).</summary>
    public required string Path { get; init; }

    /// <summary>PFX password. Required when using .pfx certificates.</summary>
    public string? Password { get; init; }

    /// <summary>PEM private key path. Required when using .pem certificates.</summary>
    public string? KeyPath { get; init; }

    public bool IsPem => KeyPath is not null;
}
