namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Server-authoritative health for humanoid bandit NPCs.</summary>
[Title( "Thorns Bandit Health" )]
[Category( "Thorns/Combat" )]
public sealed class ThornsBanditHealth : Component
{
	[Property] public float MaxHealth { get; set; } = 100f;

	[Sync( SyncFlags.FromHost )] public float CurrentHealth { get; set; } = 100f;

	[Sync( SyncFlags.FromHost )] public bool IsDeadState { get; set; }

	public bool IsAlive => CurrentHealth > 0.001f && !IsDeadState;

	protected override void OnStart()
	{
		if ( ThornsMultiplayer.IsHostOrOffline )
			HostReset();
	}

	public void HostReset()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		CurrentHealth = MaxHealth;
		IsDeadState = false;
	}

	public bool HostApplyDamage( float amount, GameObject attackerRoot, bool isHeadshot = false, string weaponId = "" )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || amount <= 0f || !IsAlive )
			return false;

		var brain = Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		var info = new ThornsCombatDamage.DamageInfo
		{
			Amount = amount,
			AttackerRoot = attackerRoot,
			VictimRoot = GameObject,
			VictimKind = ThornsCombatDamage.VictimKind.Npc,
			AttackerFaction = ThornsCombatFactions.ResolveFaction( attackerRoot ),
			VictimFaction = ThornsCombatFactions.FactionKind.Bandit,
			IsHeadshot = isHeadshot,
			DamageTypeId = "bandit_hitscan",
			WeaponId = weaponId ?? "",
			AttackerAccountKey = ThornsCombatFactions.ResolveAccountKey( attackerRoot )
		};

		if ( !ThornsCombatFactions.HostCanDamage( attackerRoot, GameObject, info ) )
			return false;

		brain?.HostNotifyDamagedByHostile( attackerRoot );

		var before = CurrentHealth;
		CurrentHealth = Math.Max( 0f, CurrentHealth - amount );
		if ( ThornsBanditDebug.LogBehaviors && CurrentHealth > 0f )
		{
			var attackerName = attackerRoot.IsValid() ? attackerRoot.Name : "unknown";
			Log.Info( $"[BanditAI] Damage {amount:F1} from {attackerName} -> HP {before:F0}->{CurrentHealth:F0}" );
		}
		if ( CurrentHealth > 0f )
			return false;

		CurrentHealth = 0f;
		IsDeadState = true;
		HostOnKilled( attackerRoot );
		return true;
	}

	void HostOnKilled( GameObject attackerRoot )
	{
		var brain = Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		brain?.HostNotifyKilled( attackerRoot );
	}
}
