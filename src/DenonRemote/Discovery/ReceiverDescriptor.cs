// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

namespace DenonRemote.Discovery;

/// <summary>Compact identity for a discovered receiver, shown in the UI.</summary>
public sealed record ReceiverDescriptor(
    string Host,
    string Manufacturer,
    string Model,
    string FriendlyName,
    string Uuid)
{
    public override string ToString() =>
        string.IsNullOrWhiteSpace(FriendlyName) ? $"{Model} ({Host})" : $"{FriendlyName} Â· {Host}";
}

