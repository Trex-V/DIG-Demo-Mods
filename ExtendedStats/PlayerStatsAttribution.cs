using DigDeliver;
using PurrNet;

namespace ExtendedStats;

internal static class PlayerStatsAttribution
{
    internal static bool IsLocalPlayer(PlayerID playerId)
    {
        // Resolve the actor ID and compare it to the local avatar for player-only totals.
        PlayerAvatar localPlayer = PlayerAvatar.Local;
        return localPlayer != null
            && PlayerAvatar.TryGet(playerId, out PlayerAvatar actor)
            && actor == localPlayer;
    }
}
