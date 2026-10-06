using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>
/// Serves the TLS certificate from a .pfx file and picks up a renewed file on the next handshake, without a restart.
/// Connections already open keep the certificate they started with until they end.
/// </summary>
public sealed class ReloadingCertificate
{
    private readonly string path;
    private readonly string? password;
    private readonly ILogger logger;
    private readonly Lock gate = new();

    private SslStreamCertificateContext current;
    private FileStamp loaded;
    private FileStamp? failed;

    /// <summary>Loads the certificate once, throwing if it cannot be read, so a bad certificate still stops startup.</summary>
    public ReloadingCertificate(string path, string? password, ILogger logger)
    {
        this.path = path;
        this.password = password;
        this.logger = logger;
        loaded = FileStamp.Of(path);
        current = Load();
    }

    /// <summary>The certificate for a new handshake; reloads it when the file has changed since it was last read.</summary>
    public SslStreamCertificateContext Current
    {
        get
        {
            var stamp = FileStamp.Of(path);
            if (stamp == loaded)
            {
                return current;
            }

            lock (gate)
            {
                // A file that failed to load is retried only once it changes again, so a broken file is logged once.
                if (stamp == loaded || stamp == failed)
                {
                    return current;
                }

                try
                {
                    current = Load();
                    loaded = stamp;
                    failed = null;
                    logger.LogInformation(
                        "Loaded the renewed TLS certificate (expires {Expiry:u}).", current.TargetCertificate.NotAfter);
                }
                catch (Exception failure)
                {
                    // A file still being written, or a broken one, keeps the certificate that worked.
                    failed = stamp;
                    logger.LogWarning(
                        "Kept the current TLS certificate: the file at '{Path}' changed but could not be loaded: {Reason}",
                        path, failure.Message);
                }

                return current;
            }
        }
    }

    // The previous context is not disposed: a handshake in progress may still be using it.
    private SslStreamCertificateContext Load()
    {
        var certificates = X509CertificateLoader.LoadPkcs12CollectionFromFile(path, password);
        var leaf = certificates.FirstOrDefault(certificate => certificate.HasPrivateKey)
            ?? throw new InvalidOperationException("The file holds no certificate with a private key.");

        // The intermediates go out with the leaf, so clients can build the chain to their trusted root.
        var intermediates = new X509Certificate2Collection();
        intermediates.AddRange(certificates.Where(certificate => certificate != leaf).ToArray());
        return SslStreamCertificateContext.Create(leaf, intermediates, offline: true);
    }

    private readonly record struct FileStamp(DateTime Modified, long Length)
    {
        public static FileStamp Of(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? new FileStamp(file.LastWriteTimeUtc, file.Length) : default;
        }
    }
}
