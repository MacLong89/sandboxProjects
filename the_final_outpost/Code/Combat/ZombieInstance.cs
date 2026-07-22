namespace FinalOutpost;

/// <summary>Runtime zombie instance — logic lives in <see cref="CombatSystem"/>.</summary>
public sealed class ZombieInstance
{
	public GameObject Go;
	public CharacterModel Character;
	public UI.WorldLabel Label;
	public ZombieTypeDef TypeDef;
	public float Health;
	public float MaxHealth;
	public float Damage;
	public float Speed;
	/// <summary>Seconds remaining of Oil Slick slow.</summary>
	public float SlowRemain;
	/// <summary>Move multiplier while slowed (e.g. 0.4).</summary>
	public float SlowMult = 1f;
	public float MoveMult => SlowRemain > 0f ? SlowMult : 1f;
	public float EffectiveSpeed => Speed * MoveMult;
	public float AttackTimer;
	public float MoveStuckTimer;
	/// <summary>Accumulated time with little or no movement progress.</summary>
	public float TotalStuckTimer;
	/// <summary>Time without getting meaningfully closer to the current target.</summary>
	public float ApproachStallTimer;
	public float LastAttackDist = float.MaxValue;
	public int StrafeSign;
	public bool HasDetour;
	public Vector3 DetourGoal;
	/// <summary>Smoothed move goal — stops approach points snapping at building corners.</summary>
	public Vector3 EngagePointSmooth;
	public bool EngagePointInit;
	public bool IsEngaged;
	public bool Dead;
	/// <summary>
	/// AUDIT FIX M6: when true, CleanupDead must NOT SpawnSplitters.
	/// Set by round failsafe kills so a stalled Splitter cannot re-seed the wave
	/// if CleanupDead runs before ClearAll (defense-in-depth; same-tick clear usually wins).
	/// </summary>
	public bool SuppressSplitOnDeath;
	/// <summary>Which perimeter edge this zombie approached from — locks breach behavior to one side.</summary>
	public WallApproachSide ApproachSide;

	// --- Wall vault (CanJumpWalls) — real arc over timber instead of ignoreWalls phasing ---
	public enum WallVaultPhase { None, Approach, Airborne }
	public WallVaultPhase VaultPhase;
	/// <summary>0..1 progress through the airborne vault arc.</summary>
	public float VaultT;
	public Vector3 VaultFrom;
	public Vector3 VaultTo;
	public float VaultDuration;
	public bool VaultJumpTriggered;

	/// <summary>True while vaulting — ONLY then may pathing ignore perimeter wall cells.</summary>
	public bool IsVaulting => VaultPhase == WallVaultPhase.Airborne;

	public Vector3 Position => Go.IsValid() ? Go.WorldPosition : Vector3.Zero;

	public bool Hit( float dmg )
	{
		if ( Dead ) return false;
		var taken = dmg * (TypeDef?.DamageTakenMult ?? 1f);
		Health = MathF.Max( 0f, Health - taken );
		RefreshLabel();
		if ( Health <= 0f )
		{
			Dead = true;
			return true;
		}

		return false;
	}

	public void ApplySlow( float mult, float duration )
	{
		if ( Dead || duration <= 0f ) return;
		mult = Math.Clamp( mult, 0.05f, 1f );
		if ( SlowRemain <= 0f || mult < SlowMult )
			SlowMult = mult;
		SlowRemain = MathF.Max( SlowRemain, duration );
	}

	public void TickSlow( float dt )
	{
		if ( SlowRemain <= 0f ) return;
		SlowRemain -= dt;
		if ( SlowRemain <= 0f )
		{
			SlowRemain = 0f;
			SlowMult = 1f;
		}
	}

	public void RefreshLabel()
	{
		if ( Label is null ) return;
		Label.Text = $"{(int)MathF.Ceiling( Health )}";
	}
}
