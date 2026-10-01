using System;

namespace DungeonMasterXIV.Net;

/// <summary>The host's answer to a join request as seen by the joiner: accepted with a host key, denied, or lapsed.</summary>
public abstract class AdmissionOutcome
{
    private AdmissionOutcome()
    {
    }

    public static AdmissionOutcome Accepted(byte[] hostPublicKey) => new AcceptedOutcome(hostPublicKey);

    public static AdmissionOutcome Denied() => DeniedOutcome.Instance;

    public static AdmissionOutcome Lapsed() => LapsedOutcome.Instance;

    public abstract T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed);

    /// <summary>An accepted answer that carries the host's public key.</summary>
    private sealed class AcceptedOutcome : AdmissionOutcome
    {
        private readonly byte[] _hostPublicKey;

        public AcceptedOutcome(byte[] hostPublicKey) => _hostPublicKey = hostPublicKey;

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onAccepted(_hostPublicKey);
    }

    /// <summary>A denied answer.</summary>
    private sealed class DeniedOutcome : AdmissionOutcome
    {
        public static readonly DeniedOutcome Instance = new();

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onDenied();
    }

    /// <summary>An answer saying the request lapsed before the host decided.</summary>
    private sealed class LapsedOutcome : AdmissionOutcome
    {
        public static readonly LapsedOutcome Instance = new();

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onLapsed();
    }
}
