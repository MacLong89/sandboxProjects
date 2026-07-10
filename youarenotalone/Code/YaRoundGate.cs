namespace Sandbox;

/// <summary>Client + host query for whether match rules allow movement / combat (driven by replicated game state).</summary>
public static class YaRoundGate
{
	/// <summary>Movement/look are always allowed; match flow only gates weapons via <see cref="MayUseWeapons"/>.</summary>
	public static bool MayMoveAndLook() => true;

	public static bool IsRoundVictoryBannerBlocking()
	{
		var gs = YaGameStateSystem.Instance;
		return gs is { IsValid: true, CurrentState: YaGameState.RoundVictory };
	}

	/// <summary>Host-authoritative damage only during an active round (intermission is free-move, no combat).</summary>
	public static bool MayDealOrTakeDamage()
	{
		var gs = YaGameStateSystem.Instance;
		if ( gs is null || !gs.IsValid() )
			return false;
		return gs.CurrentState == YaGameState.InRound;
	}

	/// <summary>When false, weapons must not fire (lobby, intermission, lobby wait, or post-round win banner).</summary>
	public static bool MayUseWeapons()
	{
		var gs = YaGameStateSystem.Instance;
		if ( gs is null || !gs.IsValid() )
			return true;
		if ( IsRoundVictoryBannerBlocking() )
			return false;
		return gs.CurrentState == YaGameState.InRound;
	}

	/// <summary>Alone dash (Q) — not a weapon; walking phases unless the round-win banner is active.</summary>
	public static bool MayUseAloneTeleport()
	{
		if ( IsRoundVictoryBannerBlocking() )
			return false;
		var gs = YaGameStateSystem.Instance;
		if ( gs is null || !gs.IsValid() )
			return true;
		return gs.CurrentState is YaGameState.Lobby or YaGameState.Intermission or YaGameState.InRound;
	}
}
