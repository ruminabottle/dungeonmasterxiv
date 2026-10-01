using System;

namespace DungeonMasterXIV.Net;

public abstract class AdmissionOutcome
{
    private AdmissionOutcome()
    {
    }

    public static AdmissionOutcome Accepted(byte[] hostPublicKey) => new AcceptedOutcome(hostPublicKey);

    public static AdmissionOutcome Denied() => DeniedOutcome.Instance;

    public static AdmissionOutcome Lapsed() => LapsedOutcome.Instance;

    public abstract T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed);

    private sealed class AcceptedOutcome : AdmissionOutcome
    {
        private readonly byte[] _hostPublicKey;

        public AcceptedOutcome(byte[] hostPublicKey) => _hostPublicKey = hostPublicKey;

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onAccepted(_hostPublicKey);
    }

    private sealed class DeniedOutcome : AdmissionOutcome
    {
        public static readonly DeniedOutcome Instance = new();

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onDenied();
    }

    private sealed class LapsedOutcome : AdmissionOutcome
    {
        public static readonly LapsedOutcome Instance = new();

        public override T Match<T>(Func<byte[], T> onAccepted, Func<T> onDenied, Func<T> onLapsed) =>
            onLapsed();
    }
}
