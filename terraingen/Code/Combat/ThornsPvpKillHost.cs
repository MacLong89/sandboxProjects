namespace Terraingen.Combat;

using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.Victory;

/// <summary>Host-only PvP kill rewards — XP, victory, streak hype, victim revenge hook.</summary>
public static class ThornsPvpKillHost
{
	public const int XpPerPlayerKill = 50;
	public const int XpPerStreakBonus = 20;

	static readonly Dictionary<string, int> KillStreakByAccount = new( StringComparer.OrdinalIgnoreCase );

	public static void HostReportPlayerKill( ThornsPlayerGameplay killer, ThornsPlayerGameplay victim )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || killer is null || victim is null )
			return;

		if ( killer == victim )
			return;

		if ( ThornsCombatSettings.BlockGuildFriendlyFire && ShareGuild( killer, victim ) )
			return;

		var victimName = ResolveDisplayName( victim );
		var killerName = ResolveDisplayName( killer );

		victim.HostSetSessionLastKiller( killerName );

		var streak = BumpKillStreak( killer.AccountKey );
		var bonusXp = streak >= 2 ? XpPerStreakBonus * (streak - 1) : 0;
		var totalXp = XpPerPlayerKill + bonusXp;

		ThornsMilestoneTracker.OnKill( killer, "player" );
		ThornsVictoryBridge.Report( killer, "pvp_victory" );
		killer.HostGrantXp( totalXp );

		var title = streak >= 2
			? $"Eliminated {victimName} — {streak} streak!"
			: $"Eliminated {victimName}";

		killer.PushMilestoneToastToOwner( title, totalXp );

		if ( streak >= 2 )
			ThornsWorldEventHudBus.PushWorldEvent( $"{killerName} is on a {streak}-kill streak!", 5f );

		victim.PushClientToastToOwner( $"Killed by {killerName}. Your loot is in a death crate.", "danger", 6f );
	}

	public static void HostResetKillStreak( string accountKey )
	{
		if ( string.IsNullOrWhiteSpace( accountKey ) )
			return;

		KillStreakByAccount.Remove( accountKey );
	}

	static int BumpKillStreak( string accountKey )
	{
		if ( string.IsNullOrWhiteSpace( accountKey ) )
			return 1;

		KillStreakByAccount.TryGetValue( accountKey, out var streak );
		streak = Math.Max( 1, streak + 1 );
		KillStreakByAccount[accountKey] = streak;
		return streak;
	}

	static string ResolveDisplayName( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay?.GameObject.IsValid() != true )
			return "Unknown";

		var name = gameplay.GameObject.Name?.Trim();
		return string.IsNullOrWhiteSpace( name ) ? "Survivor" : name;
	}

	static bool ShareGuild( ThornsPlayerGameplay a, ThornsPlayerGameplay b )
	{
		if ( a is null || b is null )
			return false;

		var keyA = a.AccountKey;
		var keyB = b.AccountKey;
		if ( string.IsNullOrWhiteSpace( keyA ) || string.IsNullOrWhiteSpace( keyB ) )
			return false;

		if ( string.Equals( keyA, keyB, StringComparison.OrdinalIgnoreCase ) )
			return true;

		var service = ThornsGuildWorldService.Instance;
		if ( service is null )
			return false;

		if ( !service.TryGetAccountGuildId( keyA, out var guildA ) || !service.TryGetAccountGuildId( keyB, out var guildB ) )
			return false;

		return guildA == guildB;
	}
}
