namespace Sandbox;

/// <summary>One pellet or single-bullet hitscan result. TracerEnd is always valid for FX.</summary>
public readonly record struct AimboxPelletResult(
	Vector3 Direction,
	Vector3 TracerEnd,
	IAimboxCombatActor HitActor,
	AimboxDummyTarget HitDummy,
	bool Headshot,
	float Damage,
	float Distance )
{
	public bool Hit => HitActor is not null || HitDummy is not null;
}

/// <summary>All pellets from one trigger pull.</summary>
public readonly record struct AimboxHitscanShotResult( IReadOnlyList<AimboxPelletResult> Pellets )
{
	public static AimboxHitscanShotResult Empty { get; } = new( Array.Empty<AimboxPelletResult>() );

	public bool AnyHit
	{
		get
		{
			foreach ( var pellet in Pellets )
			{
				if ( pellet.Hit )
					return true;
			}

			return false;
		}
	}

	public bool AnyHeadshot
	{
		get
		{
			foreach ( var pellet in Pellets )
			{
				if ( pellet.Headshot )
					return true;
			}

			return false;
		}
	}

	public float TotalDamage
	{
		get
		{
			var total = 0f;
			foreach ( var pellet in Pellets )
			{
				if ( pellet.Hit )
					total += pellet.Damage;
			}

			return total;
		}
	}
}

/// <summary>Input bundle for a single fire attempt — keeps combat authority decoupled from input.</summary>
public readonly record struct AimboxCombatShotRequest(
	IAimboxCombatActor Attacker,
	AimboxWeaponRuntime Weapon,
	Vector3 AimForward,
	bool AdsHeld,
	bool Moving,
	bool Crouched,
	bool MeleeHeavy );

/// <summary>Recoil state carried across shots for spray continuity.</summary>
public sealed class AimboxRecoilSessionState
{
	public double LastShotTime;
	public int PatternIndex;
	public int SprayOrdinal;
	readonly AimboxWeaponRecoilController _controller = new();

	public AimboxWeaponRecoilController Controller => _controller;

	public void Reset()
	{
		LastShotTime = 0;
		PatternIndex = 0;
		SprayOrdinal = 0;
		_controller.Reset();
	}
}
