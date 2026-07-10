using System;
using System.Collections.Generic;

namespace Terraingen.Combat;

/// <summary>
/// Authoritative weapon tuning (THORNS_EVERYTHING_DOCUMENT §3: fire rate host, range clamp, durability per shot, ammo).
/// Extensible for: shotgun pellet count, armor pen, attachments, affixes (via future instance payload).
/// </summary>
public static class ThornsWeaponDefinitions
{
	// Plain class (not record inside static type — avoids Sandbox codegen CS0026 on nested records).
	public sealed class WeaponDefinition
	{
		public string Id { get; init; }
		public string DisplayName { get; init; }
		public float BaseDamage { get; init; }
		public float FireIntervalSeconds { get; init; }
		public string FireMode { get; init; }
		public int ClipSize { get; init; }
		public float ReloadTimeSeconds { get; init; }
		public float MaxRange { get; init; }
		public string AmmoTypeId { get; init; }
		public float DurabilityLossPerShot { get; init; }
		public float MaxDurability { get; init; }
		public float HeadshotMultiplier { get; init; }
		public float SecondaryAttackBaseDamage { get; init; }
		public float SecondaryAttackFireIntervalSeconds { get; init; }
		public int PelletCount { get; init; }
		public float PelletSpreadHalfAngleDegrees { get; init; }
		public float RecoilPatternScaleDegrees { get; init; }
		public float RecoilResetDelaySeconds { get; init; }
		public bool RecoilPatternClampEnd { get; init; }
		public float BloomHalfAngleDegreesBase { get; init; }
		public float BloomHalfAngleDegreesPerSprayShot { get; init; }
		public float AdsRecoilMul { get; init; }
		public float AdsBloomMul { get; init; }
		public float MovingBloomMul { get; init; }
		public float MovingRecoilMul { get; init; }
		public float CrouchRecoilMul { get; init; }
		public float CrouchBloomMul { get; init; }
		public float ClientVisualKickScale { get; init; }
		public bool ReloadIsPerShellCycle { get; init; }
		public int ReloadShellCountPerRpc { get; init; }
		public int WeaponTier { get; init; }
		public float CriticalHitChanceOverride { get; init; }
		public float CriticalDamageMultiplierOverride { get; init; }

		public WeaponDefinition(
			string Id,
			string DisplayName,
			float BaseDamage,
			float FireIntervalSeconds,
			string FireMode,
			int ClipSize,
			float ReloadTimeSeconds,
			float MaxRange,
			string AmmoTypeId,
			float DurabilityLossPerShot,
			float MaxDurability,
			float HeadshotMultiplier,
			float SecondaryAttackBaseDamage = 0f,
			float SecondaryAttackFireIntervalSeconds = 1f,
			int PelletCount = 1,
			float PelletSpreadHalfAngleDegrees = 0f,
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
			float ClientVisualKickScale = 24f,
			bool ReloadIsPerShellCycle = false,
			int ReloadShellCountPerRpc = 1,
			int WeaponTier = 2,
			float CriticalHitChanceOverride = -1f,
			float CriticalDamageMultiplierOverride = -1f )
		{
			this.Id = Id;
			this.DisplayName = DisplayName;
			this.BaseDamage = BaseDamage;
			this.FireIntervalSeconds = FireIntervalSeconds;
			this.FireMode = FireMode;
			this.ClipSize = ClipSize;
			this.ReloadTimeSeconds = ReloadTimeSeconds;
			this.MaxRange = MaxRange;
			this.AmmoTypeId = AmmoTypeId;
			this.DurabilityLossPerShot = DurabilityLossPerShot;
			this.MaxDurability = MaxDurability;
			this.HeadshotMultiplier = HeadshotMultiplier;
			this.SecondaryAttackBaseDamage = SecondaryAttackBaseDamage;
			this.SecondaryAttackFireIntervalSeconds = SecondaryAttackFireIntervalSeconds;
			this.PelletCount = PelletCount;
			this.PelletSpreadHalfAngleDegrees = PelletSpreadHalfAngleDegrees;
			this.RecoilPatternScaleDegrees = RecoilPatternScaleDegrees;
			this.RecoilResetDelaySeconds = RecoilResetDelaySeconds;
			this.RecoilPatternClampEnd = RecoilPatternClampEnd;
			this.BloomHalfAngleDegreesBase = BloomHalfAngleDegreesBase;
			this.BloomHalfAngleDegreesPerSprayShot = BloomHalfAngleDegreesPerSprayShot;
			this.AdsRecoilMul = AdsRecoilMul;
			this.AdsBloomMul = AdsBloomMul;
			this.MovingBloomMul = MovingBloomMul;
			this.MovingRecoilMul = MovingRecoilMul;
			this.CrouchRecoilMul = CrouchRecoilMul;
			this.CrouchBloomMul = CrouchBloomMul;
			this.ClientVisualKickScale = ClientVisualKickScale;
			this.ReloadIsPerShellCycle = ReloadIsPerShellCycle;
			this.ReloadShellCountPerRpc = ReloadShellCountPerRpc;
			this.WeaponTier = WeaponTier;
			this.CriticalHitChanceOverride = CriticalHitChanceOverride;
			this.CriticalDamageMultiplierOverride = CriticalDamageMultiplierOverride;
		}
	}

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
			BaseDamage: 24f,
			FireIntervalSeconds: 0.095f,
			FireMode: "auto",
			ClipSize: 30,
			ReloadTimeSeconds: 1.75f,
			MaxRange: 12000f,
			AmmoTypeId: "rifle_ammo",
			DurabilityLossPerShot: 0.5f,
			MaxDurability: 100f,
			HeadshotMultiplier: 3f,
			BloomHalfAngleDegreesBase: 0.18f,
			WeaponTier: 3 ),
		["mp5"] = new WeaponDefinition(
			Id: "mp5",
			DisplayName: "MP5",
			BaseDamage: 18f,
			FireIntervalSeconds: 0.065f,
			FireMode: "auto",
			ClipSize: 34,
			ReloadTimeSeconds: 1.55f,
			MaxRange: 12000f,
			AmmoTypeId: "smg_ammo",
			DurabilityLossPerShot: 0.45f,
			MaxDurability: 100f,
			HeadshotMultiplier: 3f,
			BloomHalfAngleDegreesBase: 0.24f,
			RecoilPatternScaleDegrees: 0.22f,
			BloomHalfAngleDegreesPerSprayShot: 0.038f,
			WeaponTier: 2 ),
		["usp"] = new WeaponDefinition(
			Id: "usp",
			DisplayName: "USP",
			BaseDamage: 27f,
			FireIntervalSeconds: 0.22f,
			FireMode: "semi",
			ClipSize: 12,
			ReloadTimeSeconds: 1.35f,
			MaxRange: 12000f,
			AmmoTypeId: "pistol_ammo",
			DurabilityLossPerShot: 0.4f,
			MaxDurability: 100f,
			HeadshotMultiplier: 3f,
			BloomHalfAngleDegreesBase: 0.22f,
			RecoilPatternScaleDegrees: 0.28f,
			ClientVisualKickScale: 12f,
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
			BaseDamage: 82f,
			FireIntervalSeconds: 1.05f,
			FireMode: "semi",
			ClipSize: 5,
			ReloadTimeSeconds: 2.65f,
			MaxRange: 20000f,
			AmmoTypeId: "sniper_ammo",
			DurabilityLossPerShot: 1.2f,
			MaxDurability: 100f,
			HeadshotMultiplier: 3f,
			RecoilPatternClampEnd: true,
			BloomHalfAngleDegreesBase: 0.08f,
			WeaponTier: 4 ),
		["bow"] = new WeaponDefinition(
			Id: "bow",
			DisplayName: "Bow",
			BaseDamage: 58f,
			FireIntervalSeconds: 0.85f,
			FireMode: "bow",
			ClipSize: 1,
			ReloadTimeSeconds: 1.1f,
			MaxRange: 14000f,
			AmmoTypeId: "arrow",
			DurabilityLossPerShot: 0f,
			MaxDurability: 100f,
			HeadshotMultiplier: 2.5f,
			BloomHalfAngleDegreesBase: 0.035f,
			RecoilPatternScaleDegrees: 0.1f,
			AdsRecoilMul: 0.45f,
			AdsBloomMul: 0.35f,
			WeaponTier: 2 ),
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

		[ThornsFpToolCombat.CombatIdBareHands] = new WeaponDefinition(
			Id: ThornsFpToolCombat.CombatIdBareHands,
			DisplayName: "Bare Hands",
			BaseDamage: 10f,
			FireIntervalSeconds: 0.5f,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 184f,
			AmmoTypeId: "",
			DurabilityLossPerShot: 0f,
			MaxDurability: 100f,
			HeadshotMultiplier: 1f,
			WeaponTier: 1 ),

		[ThornsFpToolCombat.CombatIdPrimitive] = new WeaponDefinition(
			Id: ThornsFpToolCombat.CombatIdPrimitive,
			DisplayName: "Primitive tool (melee)",
			BaseDamage: 10f,
			FireIntervalSeconds: ThornsFpToolCombat.ToolMeleeLightSwingCooldownSeconds,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 184f,
			AmmoTypeId: "",
			DurabilityLossPerShot: 0f,
			MaxDurability: 100f,
			HeadshotMultiplier: 1f ),

		[ThornsFpToolCombat.CombatIdStone] = new WeaponDefinition(
			Id: ThornsFpToolCombat.CombatIdStone,
			DisplayName: "Stone tool (melee)",
			BaseDamage: 10f,
			FireIntervalSeconds: ThornsFpToolCombat.ToolMeleeLightSwingCooldownSeconds,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 190f,
			AmmoTypeId: "",
			DurabilityLossPerShot: 0f,
			MaxDurability: 100f,
			HeadshotMultiplier: 1f ),

		[ThornsFpToolCombat.CombatIdMetal] = new WeaponDefinition(
			Id: ThornsFpToolCombat.CombatIdMetal,
			DisplayName: "Metal tool (melee)",
			BaseDamage: 10f,
			FireIntervalSeconds: ThornsFpToolCombat.ToolMeleeLightSwingCooldownSeconds,
			FireMode: "melee",
			ClipSize: 0,
			ReloadTimeSeconds: 0f,
			MaxRange: 196f,
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

	public static bool IsBowWeapon( WeaponDefinition d, string authoritativeCombatId ) =>
		(d is not null && string.Equals( d.FireMode, "bow", StringComparison.OrdinalIgnoreCase ))
		|| string.Equals( (authoritativeCombatId ?? "").Trim(), "bow", StringComparison.OrdinalIgnoreCase );

	/// <summary>When <see cref="Get"/> returns a placeholder or tuning is stale, still treat known melee items as melee for host UX (ammo, reload).</summary>
	public static bool IsKnownMeleeCombatId( string combatId )
	{
		if ( string.IsNullOrWhiteSpace( combatId ) )
			return false;
		var t = combatId.Trim();
		if ( string.Equals( t, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return true;
		return ThornsFpToolCombat.IsToolMeleeCombatId( t );
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

		if ( ThornsFpToolCombat.IsToolMeleeCombatId( cid ) )
		{
			if ( string.Equals( cid, ThornsFpToolCombat.CombatIdPrimitive, StringComparison.OrdinalIgnoreCase )
			     || string.Equals( cid, ThornsFpToolCombat.CombatIdBareHands, StringComparison.OrdinalIgnoreCase ) )
				tier = 1;
			else if ( string.Equals( cid, ThornsFpToolCombat.CombatIdStone, StringComparison.OrdinalIgnoreCase ) )
				tier = 2;
			else if ( string.Equals( cid, ThornsFpToolCombat.CombatIdMetal, StringComparison.OrdinalIgnoreCase ) )
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
