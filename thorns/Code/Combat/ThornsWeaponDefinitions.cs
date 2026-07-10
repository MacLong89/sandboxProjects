using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Authoritative weapon tuning (THORNS_EVERYTHING_DOCUMENT §3: fire rate host, range clamp, durability per shot, ammo).
/// Extensible for: shotgun pellet count, armor pen, attachments, affixes (via future instance payload).
/// </summary>
public static class ThornsWeaponDefinitions
{
	/// <param name="FireMode">Placeholder: e.g. semi, auto, bolt — not simulated this milestone.</param>
	public sealed record WeaponDefinition(
		string Id,
		string DisplayName,
		float BaseDamage,
		/// <summary>Minimum seconds between shots (host-enforced).</summary>
		float FireIntervalSeconds,
		string FireMode,
		int ClipSize,
		float ReloadTimeSeconds,
		float MaxRange,
		/// <summary>Matches <see cref="ThornsItemRegistry.ThornsItemDefinition.AmmoTypeId"/> on ammo items.</summary>
		string AmmoTypeId,
		float DurabilityLossPerShot,
		float MaxDurability,
		float HeadshotMultiplier,
		/// <summary>Right-click / secondary melee: damage (use with <see cref="FireMode"/> <c>melee</c>). 0 = disabled.</summary>
		float SecondaryAttackBaseDamage = 0f,
		/// <summary>Cooldown for secondary melee (seconds).</summary>
		float SecondaryAttackFireIntervalSeconds = 1f,
		/// <summary>If &gt; 1, each fired shell resolves <see cref="BaseDamage"/> per pellet with randomized cone spread.</summary>
		int PelletCount = 1,
		/// <summary>Half-angle (degrees) from aim axis for uniform pellet spread; ignored when <see cref="PelletCount"/> is 1.</summary>
		float PelletSpreadHalfAngleDegrees = 0f,
		// --- Authoritative recoil / minimal bloom (server hit direction — THORNS §3 / §7); melee ignores in weapon code path. ---
		float RecoilPatternScaleDegrees = 0.24f,
		float RecoilResetDelaySeconds = 0.26f,
		bool RecoilPatternClampEnd = false,
		float BloomHalfAngleDegreesBase = 0.08f,
		float BloomHalfAngleDegreesPerSprayShot = 0.032f,
		float AdsRecoilMul = 0.72f,
		float AdsBloomMul = 0.58f,
		float MovingBloomMul = 1.25f,
		float MovingRecoilMul = 1.08f,
		float CrouchRecoilMul = 0.88f,
		float CrouchBloomMul = 0.82f,
		/// <summary>
		/// Multiplier from the <b>same</b> pattern step degrees used for authoritative aim (those are intentionally small — ~0.24°/unit).
		/// Without this, client camera kick is sub-degree and reads as zero in logs and on screen. Tune per weapon for feel.
		/// </summary>
		float ClientVisualKickScale = 14f,
		/// <summary>Shotgun-style: each reload RPC waits <see cref="ReloadTimeSeconds"/> then moves up to <see cref="ReloadShellCountPerRpc"/> shells from reserve (one <c>Reload_Shell</c> anim gate per RPC on FP).</summary>
		bool ReloadIsPerShellCycle = false,
		/// <summary>Shells to add after one reload RPC when <see cref="ReloadIsPerShellCycle"/> is true (usually 1).</summary>
		int ReloadShellCountPerRpc = 1,
		/// <summary>1 = lowest … 4 = best military; drives <see cref="ResolveCriticalHitChance"/> for guns. Melee uses combat id instead.</summary>
		int WeaponTier = 2,
		/// <summary>If ≥ 0, overrides tier-based crit chance; otherwise <see cref="ResolveCriticalHitChance"/> uses <see cref="WeaponTier"/>.</summary>
		float CriticalHitChanceOverride = -1f,
		/// <summary>If ≥ 0, damage multiplier when a crit procs. Otherwise <see cref="ResolveCriticalDamageMultiplier"/> uses <see cref="HeadshotMultiplier"/> when &gt; 1, else 3.</summary>
		float CriticalDamageMultiplierOverride = -1f );

	static readonly Dictionary<string, WeaponDefinition> _defs = new( StringComparer.OrdinalIgnoreCase )
	{
		["dev_placeholder"] = new WeaponDefinition(
			Id: "dev_placeholder",
			DisplayName: "Dev Placeholder",
			BaseDamage: 25f,
			FireIntervalSeconds: 0.35f,
			FireMode: "semi",
			ClipSize: 30,
			ReloadTimeSeconds: 2.0f,
			MaxRange: 12000f,
			AmmoTypeId: "rifle_ammo",
			DurabilityLossPerShot: 0.4f,
			MaxDurability: 100f,
			HeadshotMultiplier: 3f,
			WeaponTier: 1 ),
		["m4"] = new WeaponDefinition(
			Id: "m4",
			DisplayName: "M4",
			BaseDamage: 25f,
			FireIntervalSeconds: 0.12f,
			FireMode: "auto",
			ClipSize: 30,
			ReloadTimeSeconds: 2.0f,
			MaxRange: 12000f,
			AmmoTypeId: "rifle_ammo",
			DurabilityLossPerShot: 0.5f,
			MaxDurability: 100f,
			HeadshotMultiplier: 3f,
			WeaponTier: 3 ),
		["mp5"] = new WeaponDefinition(
			Id: "mp5",
			DisplayName: "MP5",
			BaseDamage: 22f,
			FireIntervalSeconds: 0.08f,
			FireMode: "auto",
			ClipSize: 30,
			ReloadTimeSeconds: 2.0f,
			MaxRange: 12000f,
			AmmoTypeId: "smg_ammo",
			DurabilityLossPerShot: 0.45f,
			MaxDurability: 100f,
			HeadshotMultiplier: 3f,
			WeaponTier: 2 ),
		["shotgun"] = new WeaponDefinition(
			Id: "shotgun",
			DisplayName: "Shotgun",
			BaseDamage: 14f,
			FireIntervalSeconds: 0.75f,
			FireMode: "semi",
			ClipSize: 8,
			ReloadTimeSeconds: 0.52f,
			MaxRange: 3200f,
			AmmoTypeId: "shotgun_ammo",
			DurabilityLossPerShot: 1.1f,
			MaxDurability: 100f,
			HeadshotMultiplier: 2f,
			PelletCount: 8,
			PelletSpreadHalfAngleDegrees: 3.5f,
			ReloadIsPerShellCycle: true,
			ReloadShellCountPerRpc: 1,
			WeaponTier: 2 ),
		["sniper"] = new WeaponDefinition(
			Id: "sniper",
			DisplayName: "Sniper",
			BaseDamage: 80f,
			FireIntervalSeconds: 1.1f,
			FireMode: "semi",
			ClipSize: 5,
			ReloadTimeSeconds: 2.8f,
			MaxRange: 20000f,
			AmmoTypeId: "sniper_ammo",
			DurabilityLossPerShot: 1.2f,
			MaxDurability: 100f,
			HeadshotMultiplier: 3f,
			RecoilPatternClampEnd: true,
			BloomHalfAngleDegreesBase: 0.065f,
			WeaponTier: 4 ),
		["m9_bayonet"] = new WeaponDefinition(
			Id: "m9_bayonet",
			DisplayName: "M9 Bayonet",
			BaseDamage: 18f,
			FireIntervalSeconds: 0.175f,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 90f,
			AmmoTypeId: "",
			DurabilityLossPerShot: 0.38f,
			MaxDurability: 100f,
			HeadshotMultiplier: 1f,
			SecondaryAttackBaseDamage: 72f,
			SecondaryAttackFireIntervalSeconds: 1.15f ),

		[ThornsToolMeleeCombat.CombatIdPrimitive] = new WeaponDefinition(
			Id: ThornsToolMeleeCombat.CombatIdPrimitive,
			DisplayName: "Primitive tool (melee)",
			BaseDamage: 10f,
			FireIntervalSeconds: ThornsToolMeleeCombat.ToolMeleeLightSwingCooldownSeconds,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 92f,
			AmmoTypeId: "",
			DurabilityLossPerShot: 0f,
			MaxDurability: 100f,
			HeadshotMultiplier: 1f ),

		[ThornsToolMeleeCombat.CombatIdStone] = new WeaponDefinition(
			Id: ThornsToolMeleeCombat.CombatIdStone,
			DisplayName: "Stone tool (melee)",
			BaseDamage: 15f,
			FireIntervalSeconds: ThornsToolMeleeCombat.ToolMeleeLightSwingCooldownSeconds,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 95f,
			AmmoTypeId: "",
			DurabilityLossPerShot: 0f,
			MaxDurability: 100f,
			HeadshotMultiplier: 1f ),

		[ThornsToolMeleeCombat.CombatIdMetal] = new WeaponDefinition(
			Id: ThornsToolMeleeCombat.CombatIdMetal,
			DisplayName: "Metal tool (melee)",
			BaseDamage: 20f,
			FireIntervalSeconds: ThornsToolMeleeCombat.ToolMeleeLightSwingCooldownSeconds,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 98f,
			AmmoTypeId: "",
			DurabilityLossPerShot: 0f,
			MaxDurability: 100f,
			HeadshotMultiplier: 1f ),
	};

	static bool _mergedLateBoundCombat;
	static void MergeLateBoundCombatDefinitionsIfMissing()
	{
		if ( _mergedLateBoundCombat )
			return;

		_mergedLateBoundCombat = true;

		_defs.TryAdd(
			"mp5",
			new WeaponDefinition(
				Id: "mp5",
				DisplayName: "MP5",
				BaseDamage: 22f,
				FireIntervalSeconds: 0.08f,
				FireMode: "auto",
				ClipSize: 30,
				ReloadTimeSeconds: 2.0f,
				MaxRange: 12000f,
				AmmoTypeId: "smg_ammo",
				DurabilityLossPerShot: 0.45f,
				MaxDurability: 100f,
				HeadshotMultiplier: 3f,
				WeaponTier: 2 ) );

		_defs.TryAdd(
			"shotgun",
			new WeaponDefinition(
				Id: "shotgun",
				DisplayName: "Shotgun",
				BaseDamage: 14f,
				FireIntervalSeconds: 0.75f,
				FireMode: "semi",
				ClipSize: 8,
				ReloadTimeSeconds: 0.52f,
				MaxRange: 3200f,
				AmmoTypeId: "shotgun_ammo",
				DurabilityLossPerShot: 1.1f,
				MaxDurability: 100f,
				HeadshotMultiplier: 2f,
				PelletCount: 8,
				PelletSpreadHalfAngleDegrees: 3.5f,
				ReloadIsPerShellCycle: true,
				ReloadShellCountPerRpc: 1,
				WeaponTier: 2 ) );

		_defs.TryAdd(
			"sniper",
			new WeaponDefinition(
				Id: "sniper",
				DisplayName: "Sniper",
				BaseDamage: 80f,
				FireIntervalSeconds: 1.1f,
				FireMode: "semi",
				ClipSize: 5,
				ReloadTimeSeconds: 2.8f,
				MaxRange: 20000f,
				AmmoTypeId: "sniper_ammo",
				DurabilityLossPerShot: 1.2f,
				MaxDurability: 100f,
				HeadshotMultiplier: 3f,
				RecoilPatternClampEnd: true,
				BloomHalfAngleDegreesBase: 0.065f,
				WeaponTier: 4 ) );

		_defs.TryAdd(
			"m9_bayonet",
			new WeaponDefinition(
				Id: "m9_bayonet",
				DisplayName: "M9 Bayonet",
				BaseDamage: 18f,
				FireIntervalSeconds: 0.175f,
				FireMode: "melee",
				ClipSize: 0,
				ReloadTimeSeconds: 0f,
				MaxRange: 90f,
				AmmoTypeId: "",
				DurabilityLossPerShot: 0.38f,
				MaxDurability: 100f,
				HeadshotMultiplier: 1f,
				SecondaryAttackBaseDamage: 72f,
				SecondaryAttackFireIntervalSeconds: 1.15f ) );

		// Authoritative overrides: avoids stale assemblies / tooling leaving bulk shotgun reload or non-melee bayonet rows.
		_defs["shotgun"] = new WeaponDefinition(
			Id: "shotgun",
			DisplayName: "Shotgun",
			BaseDamage: 14f,
			FireIntervalSeconds: 0.75f,
			FireMode: "semi",
			ClipSize: 8,
			ReloadTimeSeconds: 0.52f,
			MaxRange: 3200f,
			AmmoTypeId: "shotgun_ammo",
			DurabilityLossPerShot: 1.1f,
			MaxDurability: 100f,
			HeadshotMultiplier: 2f,
			PelletCount: 8,
			PelletSpreadHalfAngleDegrees: 3.5f,
			ReloadIsPerShellCycle: true,
			ReloadShellCountPerRpc: 1,
			WeaponTier: 2 );

		_defs["m9_bayonet"] = new WeaponDefinition(
			Id: "m9_bayonet",
			DisplayName: "M9 Bayonet",
			BaseDamage: 18f,
			FireIntervalSeconds: 0.175f,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 90f,
			AmmoTypeId: "",
			DurabilityLossPerShot: 0.38f,
			MaxDurability: 100f,
			HeadshotMultiplier: 1f,
			SecondaryAttackBaseDamage: 72f,
			SecondaryAttackFireIntervalSeconds: 1.15f );
	}

	public static bool IsMeleeWeapon( WeaponDefinition d ) =>
		d is not null && string.Equals( d.FireMode, "melee", StringComparison.OrdinalIgnoreCase );

	/// <summary>When <see cref="Get"/> returns a placeholder or tuning is stale, still treat known melee items as melee for host UX (ammo, reload).</summary>
	public static bool IsKnownMeleeCombatId( string combatId )
	{
		if ( string.IsNullOrWhiteSpace( combatId ) )
			return false;
		var t = combatId.Trim();
		if ( string.Equals( t, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return true;
		return ThornsToolMeleeCombat.IsToolMeleeCombatId( t );
	}

	/// <summary>Tactical shotgun: force shell-by-shell behaviour even if <see cref="WeaponDefinition"/> did not hot-reload with <see cref="WeaponDefinition.ReloadIsPerShellCycle"/>.</summary>
	public static bool IsKnownShotgunTubeReloadId( string combatId ) =>
		!string.IsNullOrWhiteSpace( combatId )
		&& string.Equals( combatId.Trim(), "shotgun", StringComparison.OrdinalIgnoreCase );

	public static bool TreatsAsMeleeWeapon( WeaponDefinition d, string authoritativeCombatId ) =>
		IsMeleeWeapon( d ) || IsKnownMeleeCombatId( authoritativeCombatId );

	public static bool UsesPerShellReloadCycle( WeaponDefinition d, string authoritativeCombatId ) =>
		d.ReloadIsPerShellCycle || IsKnownShotgunTubeReloadId( authoritativeCombatId );

	/// <summary>0–1 crit chance for player weapons; melee tiers from combat id, guns from <see cref="WeaponDefinition.WeaponTier"/>.</summary>
	public static float ResolveCriticalHitChance( WeaponDefinition def, string authoritativeCombatId )
	{
		if ( def is null )
			return 0f;

		if ( def.CriticalHitChanceOverride >= 0f )
			return Math.Clamp( def.CriticalHitChanceOverride, 0f, 1f );

		var tier = Math.Clamp( def.WeaponTier, 1, 4 );
		var cid = (authoritativeCombatId ?? "").Trim();

		if ( ThornsToolMeleeCombat.IsToolMeleeCombatId( cid ) )
		{
			if ( string.Equals( cid, ThornsToolMeleeCombat.CombatIdPrimitive, StringComparison.OrdinalIgnoreCase ) )
				tier = 1;
			else if ( string.Equals( cid, ThornsToolMeleeCombat.CombatIdStone, StringComparison.OrdinalIgnoreCase ) )
				tier = 2;
			else if ( string.Equals( cid, ThornsToolMeleeCombat.CombatIdMetal, StringComparison.OrdinalIgnoreCase ) )
				tier = 3;
		}
		else if ( string.Equals( cid, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			tier = 4;

		return tier switch
		{
			1 => 0.05f,
			2 => 0.085f,
			3 => 0.12f,
			_ => 0.16f
		};
	}

	/// <summary>Crit body damage — matches <see cref="WeaponDefinition.HeadshotMultiplier"/> for guns; melee with HS mult 1 still crits at ×3 unless overridden.</summary>
	public static float ResolveCriticalDamageMultiplier( WeaponDefinition def )
	{
		if ( def is null )
			return 3f;

		if ( def.CriticalDamageMultiplierOverride >= 0f )
			return Math.Max( 0.01f, def.CriticalDamageMultiplierOverride );

		return def.HeadshotMultiplier > 1.01f ? def.HeadshotMultiplier : 3f;
	}

	/// <summary>Host wait per shell insert: old defs may still have ~2.6s full-clip timing — clamp for known shotgun id.</summary>
	public static float ShellReloadGameplayGateSeconds( WeaponDefinition d, string authoritativeCombatId )
	{
		var gate = Math.Max( 0.01f, d.ReloadTimeSeconds );
		if ( IsKnownShotgunTubeReloadId( authoritativeCombatId ) && d.ReloadTimeSeconds > 1f )
			return 0.52f;
		return gate;
	}

	public static bool HasSecondaryMeleeAttack( WeaponDefinition d ) =>
		d is not null && d.SecondaryAttackBaseDamage > 0f && IsMeleeWeapon( d );

	/// <summary>Heavy attack allowed when tuning resolves, or for IDs with a known heavy profile if <see cref="Get"/> returned a placeholder.</summary>
	public static bool HasSecondaryMeleeResolved( WeaponDefinition d, string authoritativeCombatId ) =>
		HasSecondaryMeleeAttack( d )
		|| (!string.IsNullOrWhiteSpace( authoritativeCombatId )
		    && string.Equals( authoritativeCombatId.Trim(), "m9_bayonet", StringComparison.OrdinalIgnoreCase ));

	public static WeaponDefinition Get( string id )
	{
		MergeLateBoundCombatDefinitionsIfMissing();

		if ( string.IsNullOrWhiteSpace( id ) )
			id = "dev_placeholder";
		else
			id = id.Trim();

		if ( string.IsNullOrEmpty( id ) )
			id = "dev_placeholder";

		if ( _defs.TryGetValue( id, out var d ) )
			return d;

		if ( string.Equals( id, "sniper", StringComparison.OrdinalIgnoreCase ) )
		{
			return new WeaponDefinition(
				Id: "sniper",
				DisplayName: "Sniper",
				BaseDamage: 80f,
				FireIntervalSeconds: 1.1f,
				FireMode: "semi",
				ClipSize: 5,
				ReloadTimeSeconds: 2.8f,
				MaxRange: 20000f,
				AmmoTypeId: "sniper_ammo",
				DurabilityLossPerShot: 1.2f,
				MaxDurability: 100f,
				HeadshotMultiplier: 3f,
				RecoilPatternClampEnd: true,
				BloomHalfAngleDegreesBase: 0.065f,
				WeaponTier: 4 );
		}

		return _defs["dev_placeholder"];
	}

	/// <summary>
	/// Stock weapon anim graph <c>firing_mode</c> enum (doc: 0=safety, 1=single, 2=burst, 3=auto).
	/// </summary>
	public static int FiringModeGraphValue( WeaponDefinition def )
	{
		if ( def is null )
			return 1;

		if ( IsMeleeWeapon( def ) )
			return 1;

		if ( string.Equals( def.FireMode, "auto", StringComparison.OrdinalIgnoreCase ) )
			return 3;
		if ( string.Equals( def.FireMode, "burst", StringComparison.OrdinalIgnoreCase ) )
			return 2;

		return 1;
	}
}
