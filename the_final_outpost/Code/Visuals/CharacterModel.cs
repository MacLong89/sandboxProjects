using Sandbox.Citizen;

namespace FinalOutpost;

/// <summary>
/// Reusable humanoid visual built from the stock citizen model + <see cref="CitizenAnimationHelper"/>,
/// mirroring aimbox / terraingen's third-person presentation. Optionally carries a world weapon that is
/// re-aligned to the right-hand bone every frame in <see cref="OnPreRender"/> (after the skeleton is
/// posed) — the same approach both reference games use to keep the gun glued to the hand instead of
/// teleporting. Motion + facing are pushed in by callers via <see cref="Tick"/>.
/// </summary>
public sealed class CharacterModel : Component
{
	public const string CitizenVmdl = "models/citizen/citizen.vmdl";

	private static readonly string[] HandBones = { "hand_R", "Hold_R", "wrist_R", "weapon_hand_R" };
	// sbox w_* meshes are authored sideways relative to the citizen rifle hand bones.
	private static readonly Angles WeaponHandEuler = new( 0f, 0f, -90f );
	// Hand-bone local nudge: +X forward, +Y toward body centre, -Z down.
	private static readonly Vector3 WeaponHandOffset = new( 10f, -6f, -3f );
	// Fallback when no hand bone yet: park it relative to the body so it never sits at the feet.
	private static readonly Vector3 BodyFallbackOffset = new( 18f, -12f, 18f );

	private SkinnedModelRenderer _skin;
	private CitizenAnimationHelper _anim;
	private GameObject _weapon;
	private SkinnedModelRenderer _weaponRenderer;
	private float _weaponScale = 1.5f;
	private CitizenAnimationHelper.HoldTypes _hold = CitizenAnimationHelper.HoldTypes.None;

	private float _zombieDuck;
	private float _zombieAnimSpeed = 1f;
	private CitizenAnimationHelper.MoveStyles _zombieMoveStyle = CitizenAnimationHelper.MoveStyles.Walk;
	private bool _isZombie;
	private bool _zombieEngaged;
	private bool _zombieVaulting;

	private Vector3 _velocity;
	private Rotation _aim;
	private bool _aimInit;

	/// <summary>Approximate muzzle tip in world space, derived from the attached weapon.</summary>
	public Vector3 MuzzleWorld( Rotation aim )
	{
		if ( _weapon.IsValid() )
			return _weapon.WorldPosition + aim.Forward * 20f + Vector3.Up * 2f;

		return WorldPosition + Vector3.Up * 52f + aim.Forward * 24f;
	}

	public void SetVisible( bool visible )
	{
		if ( _skin.IsValid() )
			_skin.Enabled = visible;
		if ( _weaponRenderer.IsValid() )
			_weaponRenderer.Enabled = visible;
		if ( _weapon.IsValid() )
			_weapon.Enabled = visible;
	}

	public void Setup( Color tint, string weaponModel, CitizenAnimationHelper.HoldTypes hold, float weaponScale = 1.5f )
	{
		_hold = hold;
		_weaponScale = weaponScale;
		_zombieDuck = 0f;
		_zombieAnimSpeed = 1f;
		_zombieMoveStyle = CitizenAnimationHelper.MoveStyles.Walk;

		var body = new GameObject( GameObject, true, "Body" );
		_skin = body.Components.Create<SkinnedModelRenderer>();
		var citizen = AssetSafe.Model( CitizenVmdl );
		if ( citizen is null )
		{
			// Extremely rare (engine base content missing) — stand-in humanoid silhouette.
			citizen = MeshPrimitives.Box;
			Log.Warning( "[FinalOutpost] Citizen model missing — using box stand-in for humanoids." );
		}

		_skin.Model = citizen;
		_skin.Tint = tint;
		_skin.UseAnimGraph = citizen != MeshPrimitives.Box;
		_skin.CreateBoneObjects = citizen != MeshPrimitives.Box;

		_anim = body.Components.Create<CitizenAnimationHelper>();
		_anim.Target = _skin;

		if ( citizen == MeshPrimitives.Box )
			body.LocalScale = new Vector3( 28f, 28f, 72f );

		if ( !string.IsNullOrWhiteSpace( weaponModel ) )
		{
			_weaponPath = weaponModel;
			_weapon = new GameObject( GameObject, true, "WeaponWorld" );
			_weapon.LocalScale = new Vector3( _weaponScale, _weaponScale, _weaponScale );
			_weaponRenderer = _weapon.Components.Create<SkinnedModelRenderer>();
			_weaponRenderer.UseAnimGraph = false;
			_weaponRenderer.Tint = Color.White;
			ApplyWeaponModel();
		}
	}

	public void RefreshWeaponModel() => ApplyWeaponModel();

	private string _weaponPath;

	private void ApplyWeaponModel()
	{
		if ( !_weaponRenderer.IsValid() || string.IsNullOrWhiteSpace( _weaponPath ) )
			return;

		_weaponRenderer.Model = WeaponModels.Load( _weaponPath );
	}

	public void SetupZombie( ZombieTypeDef def )
	{
		_isZombie = true;
		_zombieEngaged = false;
		_zombieVaulting = false;
		Setup( def.Tint, null, CitizenAnimationHelper.HoldTypes.None );
		_zombieDuck = def.DuckLevel;
		_zombieAnimSpeed = def.AnimSpeedMult;
		_zombieMoveStyle = def.MoveStyle;
	}

	public void SetZombieEngaged( bool engaged ) => _zombieEngaged = engaged;

	/// <summary>
	/// Wall vault presentation — airborne = not grounded + optional one-shot TriggerJump.
	/// </summary>
	public void SetWallVaulting( bool airborne, bool triggerJump = false )
	{
		_zombieVaulting = airborne;
		if ( airborne && triggerJump && _anim.IsValid() )
			_anim.TriggerJump();
	}

	public void TriggerMeleeSwing()
	{
		if ( _anim.IsValid() && _anim.Target.IsValid() )
			_anim.Target.Set( "b_attack", true );
	}

	/// <summary>Animation tick for zombies — state is applied in <see cref="OnUpdate"/>.</summary>
	public void TickZombie( Vector3 velocity, Rotation aim )
	{
		_velocity = velocity * _zombieAnimSpeed;
		_aim = aim;
		_aimInit = true;
	}

	public void SetTint( Color tint )
	{
		if ( _skin.IsValid() )
			_skin.Tint = tint;
	}

	/// <summary>Push the desired movement velocity + facing for this frame.</summary>
	public void Tick( Vector3 velocity, Rotation aim )
	{
		_velocity = velocity;
		_aim = aim;
		_aimInit = true;
	}

	protected override void OnUpdate()
	{
		if ( !_anim.IsValid() || !_skin.IsValid() || !_skin.UseAnimGraph )
			return;

		if ( !_aimInit )
		{
			_aim = GameObject.WorldRotation;
			_aimInit = true;
		}

		_anim.IsGrounded = !_zombieVaulting;

		if ( _isZombie )
		{
			_zombieEngaged = _zombieEngaged && !_zombieVaulting;
			_anim.DuckLevel = _zombieDuck;
			_anim.MoveStyle = _zombieMoveStyle;
			_anim.AimAngle = _aim;

			if ( _zombieVaulting )
			{
				_anim.HoldType = CitizenAnimationHelper.HoldTypes.None;
				_anim.AimBodyWeight = 0.2f;
				_anim.AimHeadWeight = 0.4f;
				_anim.WithWishVelocity( _velocity );
				_anim.WithVelocity( _velocity + Vector3.Up * 80f );
			}
			else if ( _zombieEngaged )
			{
				// Press-in melee pose instead of snapping to a hard idle (prevents foot shuffle jitter).
				var press = _aim.Forward * 42f;
				_anim.HoldType = CitizenAnimationHelper.HoldTypes.Punch;
				_anim.IsWeaponLowered = false;
				_anim.AimBodyWeight = 0.15f;
				_anim.AimHeadWeight = 0.35f;
				_anim.WithWishVelocity( press );
				_anim.WithVelocity( press * 0.3f );
			}
			else
			{
				_anim.HoldType = CitizenAnimationHelper.HoldTypes.None;
				_anim.AimBodyWeight = 0.45f;
				_anim.AimHeadWeight = 0.55f;
				_anim.WithWishVelocity( _velocity );
				_anim.WithVelocity( _velocity );
			}

			return;
		}

		_anim.WithVelocity( _velocity );
		_anim.WithWishVelocity( _velocity );
		_anim.DuckLevel = 0f;
		_anim.AimAngle = _aim;
		_anim.HoldType = _hold;
	}

	/// <summary>
	/// Runs after the skeleton has been posed for this frame, so the hand bone transform is final.
	/// Re-parents + re-aligns the weapon to the hand every frame (survives bone-object rebuilds).
	/// </summary>
	protected override void OnPreRender()
	{
		if ( !_weapon.IsValid() )
			return;

		var scale = new Vector3( _weaponScale, _weaponScale, _weaponScale );
		var attached = TryAlignToHand( scale );

		if ( !attached )
			ParentToBodyFallback( scale );

		if ( _anim.IsValid() )
			_anim.IkRightHand = attached ? null : _weapon;
	}

	private bool TryAlignToHand( Vector3 scale )
	{
		if ( !_skin.IsValid() || !_skin.Model.IsValid() )
			return false;

		_skin.CreateBoneObjects = true;

		foreach ( var boneName in HandBones )
		{
			if ( !TryGetBone( boneName, out var bone ) )
				continue;

			if ( _weapon.Parent != bone )
				_weapon.SetParent( bone );

			_weapon.LocalRotation = Rotation.From( WeaponHandEuler );
			_weapon.LocalPosition = WeaponHandOffset;
			_weapon.LocalScale = scale;
			return true;
		}

		return false;
	}

	private bool TryGetBone( string boneName, out GameObject bone )
	{
		bone = default;
		if ( !_skin.IsValid() || !_skin.Model.IsValid() )
			return false;

		if ( _skin.Model.Bones.HasBone( boneName ) )
		{
			bone = _skin.GetBoneObject( _skin.Model.Bones.GetBone( boneName ) );
			if ( bone.IsValid() && bone.Scene is not null )
				return true;
		}

		bone = _skin.GetBoneObject( boneName );
		return bone.IsValid() && bone.Scene is not null;
	}

	private void ParentToBodyFallback( Vector3 scale )
	{
		var body = _skin.IsValid() ? _skin.GameObject : GameObject;
		if ( _weapon.Parent != body )
			_weapon.SetParent( body );

		_weapon.LocalPosition = BodyFallbackOffset;
		_weapon.LocalRotation = Rotation.From( WeaponHandEuler );
		_weapon.LocalScale = scale;
	}
}
