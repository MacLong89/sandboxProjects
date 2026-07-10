namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Buildings;
using Terraingen.Core;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Host-authoritative radial explosion — structures, players, NPCs.</summary>
public static class ThornsCombatExplosion
{
	public const float DefaultRadius = 440f;
	public const float DefaultMaxPlayerDamage = 320f;
	public const float DefaultMaxStructureDamage = 520f;
	public const float DefaultMaxNpcDamage = 280f;

	public static void HostDetonate(
		Vector3 center,
		GameObject attackerRoot,
		float radius = DefaultRadius,
		float maxPlayerDamage = DefaultMaxPlayerDamage,
		float maxStructureDamage = DefaultMaxStructureDamage,
		float maxNpcDamage = DefaultMaxNpcDamage )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		radius = MathF.Max( 64f, radius );
		ThornsBanditCommunication.HostRegisterExplosion( center );
		ThornsGameplaySfx.PlayNetworkedWorldInteraction( center, ThornsGameplaySfx.Demolish, 1.35f );

		HostDamageStructures( center, radius, maxStructureDamage, attackerRoot );
		HostDamagePlayers( center, radius, maxPlayerDamage, attackerRoot );
		HostDamageBandits( center, radius, maxNpcDamage, attackerRoot );
		HostDamageWildlife( center, radius, maxNpcDamage * 0.85f, attackerRoot );
	}

	static void HostDamageStructures( Vector3 center, float radius, float maxDamage, GameObject attackerRoot )
	{
		var radiusSq = radius * radius;
		foreach ( var structure in ThornsPlacedBuildStructure.Registry.ToArray() )
		{
			if ( structure is null || !structure.IsValid() || !structure.GameObject.IsValid() )
				continue;

			if ( !ThornsStructureHealthRules.HasHealth( structure.StructureId ) )
				continue;

			var delta = structure.GameObject.WorldPosition - center;
			var distSq = delta.WithZ( 0f ).LengthSquared;
			if ( distSq > radiusSq )
				continue;

			var falloff = 1f - MathF.Sqrt( distSq ) / radius;
			if ( falloff <= 0.01f )
				continue;

			structure.HostTakeStructureDamage( maxDamage * falloff, attackerRoot );
		}
	}

	static void HostDamagePlayers( Vector3 center, float radius, float maxDamage, GameObject attackerRoot )
	{
		ThornsPlayerRootCache.RefreshIfStale( Game.ActiveScene );
		var roots = ThornsPlayerRootCache.RootsReadOnly;
		var radiusSq = radius * radius;

		for ( var i = 0; i < roots.Count; i++ )
		{
			var root = roots[i];
			if ( !root.IsValid() )
				continue;

			var health = root.Components.Get<ThornsPlayerHealth>( FindMode.EnabledInSelf );
			if ( !health.IsValid() || !health.IsAlive || health.IsDeadState )
				continue;

			var distSq = (root.WorldPosition.WithZ( 0f ) - center.WithZ( 0f )).LengthSquared;
			if ( distSq > radiusSq )
				continue;

			var falloff = 1f - MathF.Sqrt( distSq ) / radius;
			var amount = maxDamage * falloff;
			if ( amount <= 0.5f )
				continue;

			ThornsCombatDamage.HostApplyDamage(
				attackerRoot,
				root,
				new ThornsCombatDamage.DamageInfo
				{
					Amount = amount,
					AttackerRoot = attackerRoot,
					VictimRoot = root,
					DamageTypeId = "explosion",
					VictimKind = ThornsCombatDamage.VictimKind.Player,
					AttackerFaction = ThornsCombatFactions.ResolveFaction( attackerRoot ),
					VictimFaction = ThornsCombatFactions.FactionKind.Player,
					HitPosition = root.WorldPosition
				} );
		}
	}

	static void HostDamageBandits( Vector3 center, float radius, float maxDamage, GameObject attackerRoot )
	{
		var radiusSq = radius * radius;
		var brains = ThornsBanditPopulation.HostBrainsReadOnly;
		for ( var i = 0; i < brains.Count; i++ )
		{
			var brain = brains[i];
			if ( !brain.IsValid() || brain.IsDead || !brain.GameObject.IsValid() )
				continue;

			var distSq = (brain.GameObject.WorldPosition.WithZ( 0f ) - center.WithZ( 0f )).LengthSquared;
			if ( distSq > radiusSq )
				continue;

			var falloff = 1f - MathF.Sqrt( distSq ) / radius;
			var amount = maxDamage * falloff;
			if ( amount <= 0.5f )
				continue;

			ThornsCombatDamage.HostApplyDamage(
				attackerRoot,
				brain.GameObject,
				new ThornsCombatDamage.DamageInfo
				{
					Amount = amount,
					AttackerRoot = attackerRoot,
					VictimRoot = brain.GameObject,
					DamageTypeId = "explosion",
					VictimKind = ThornsCombatDamage.VictimKind.Npc,
					AttackerFaction = ThornsCombatFactions.ResolveFaction( attackerRoot ),
					VictimFaction = ThornsCombatFactions.FactionKind.Bandit,
					HitPosition = brain.GameObject.WorldPosition
				} );
		}
	}

	static void HostDamageWildlife( Vector3 center, float radius, float maxDamage, GameObject attackerRoot )
	{
		var radiusSq = radius * radius;
		foreach ( var brain in Game.ActiveScene?.GetAllComponents<ThornsAnimalBrain>() ?? Array.Empty<ThornsAnimalBrain>() )
		{
			if ( !brain.IsValid() || !brain.GameObject.IsValid() || brain.IsDead )
				continue;

			var distSq = (brain.GameObject.WorldPosition.WithZ( 0f ) - center.WithZ( 0f )).LengthSquared;
			if ( distSq > radiusSq )
				continue;

			var falloff = 1f - MathF.Sqrt( distSq ) / radius;
			var amount = maxDamage * falloff;
			if ( amount <= 0.5f )
				continue;

			ThornsCombatDamage.HostApplyDamage(
				attackerRoot,
				brain.GameObject,
				new ThornsCombatDamage.DamageInfo
				{
					Amount = amount,
					AttackerRoot = attackerRoot,
					VictimRoot = brain.GameObject,
					DamageTypeId = "explosion",
					VictimKind = ThornsCombatDamage.VictimKind.Animal,
					AttackerFaction = ThornsCombatFactions.ResolveFaction( attackerRoot ),
					VictimFaction = ThornsCombatFactions.FactionKind.Wildlife,
					HitPosition = brain.GameObject.WorldPosition
				} );
		}
	}
}
