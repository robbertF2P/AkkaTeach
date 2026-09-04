namespace AkkaTeach.Core.Actors;

/// <summary>
/// Command to wire up a full peer network — every actor learns every other actor's address.
/// </summary>
internal sealed record WirePeerNetworkCommand(IReadOnlyList<PeerRegistration> Peers);
