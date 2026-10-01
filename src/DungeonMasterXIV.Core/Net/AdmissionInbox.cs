using System;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>Queues incoming relay frames and applies them a few at a time, returning any session key agreed.</summary>
public sealed class AdmissionInbox
{
    private const int FramesPerDrain = 8;

    private readonly object _gate = new();
    private readonly Queue<byte[]> _frames = new();

    public void Receive(byte[] frame)
    {
        lock (_gate)
        {
            _frames.Enqueue(frame);
        }
    }

    public byte[]? Drain(
        JoinAttempt attempt,
        SessionKeyExchange? keys,
        HostSession? host = null,
        InboundHandlers handlers = default,
        ISessionTransportLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var arriving = new InboundFrame(attempt, keys, host, handlers, log);
        byte[]? sessionKey = null;

        foreach (var frame in TakeSlice())
        {
            if (!EnvelopeCodec.TryDecode(frame, out var envelope) || envelope is null)
            {
                continue;
            }

            sessionKey = arriving.Apply(envelope, sessionKey);
        }

        return sessionKey;
    }

    private byte[][] TakeSlice()
    {
        lock (_gate)
        {
            var taking = Math.Min(_frames.Count, FramesPerDrain);
            var frames = new byte[taking][];

            for (var i = 0; i < taking; i++)
            {
                frames[i] = _frames.Dequeue();
            }

            return frames;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _frames.Clear();
        }
    }

}
