using Sandbox.Citizen;

namespace RunGun;

/// <summary>
/// Third-person Citizen presentation shared by the runner and the enemies. Builds an animated
/// Citizen body holding a rifle and mounts a world gun model onto the right hand. Movement
/// animation is derived from the owner's frame-to-frame position delta, because this game drives
/// transforms directly instead of going through a character controller.
/// </summary>
public sealed class CitizenVisual : Component
{
	public const string CitizenModel = "models/citizen/citizen.vmdl";
	public const string DefaultGunModel = "models/weapons/sbox_assault_m4a1/w_m4a1.vmdl";

	/// <summary>Skin tint applied to the citizen body (team color).</summary>
	public Color BodyTint { get; set; } = Color.White;

	/// <summary>Uniform scale for the whole character — enemies read as bigger brutes.</summary>
	public float BodyScale { get; set; } = 1f;

	/// <summary>World gun model path; empty leaves the character unarmed.</summary>
	public string GunModel { get; set; } = DefaultGunModel;

	/// <summary>sbox w_* meshes are authored sideways relative to the rifle hand bone.</summary>
	private static readonly Angles GunHandEuler = new( 0f, 0f, -90f );
	private static readonly Vector3 GunHandOffset = new( 10f, -6f, -3f );

	private static readonly string[] RightHandBones = { "hand_R", "Hold_R", "wrist_R", "weapon_hand_R" };

	private SkinnedModelRenderer _skin;
	private CitizenAnimationHelper _anim;
	private GameObject _weaponGo;
	private bool _handAttached;

	private Vector3 _lastPos;
	private bool _hasLastPos;

	protected override void OnStart()
	{
		BuildBody();
		BuildWeapon();
	}

	private void BuildBody()
	{
		var bodyGo = new GameObject( GameObject, true, "Body" );
		bodyGo.LocalPosition = Vector3.Zero;
		bodyGo.LocalRotation = Rotation.Identity;
		bodyGo.LocalScale = Vector3.One * BodyScale;

		_skin = bodyGo.Components.Create<SkinnedModelRenderer>();
		_skin.Model = Model.Load( CitizenModel );
		_skin.Tint = BodyTint;
		_skin.UseAnimGraph = true;
		_skin.CreateBoneObjects = true;

		_anim = bodyGo.Components.Create<CitizenAnimationHelper>();
		_anim.Target = _skin;
	}

	private void BuildWeapon()
	{
		if ( string.IsNullOrWhiteSpace( GunModel ) )
			return;

		var model = Model.Load( GunModel );
		if ( !model.IsValid() || model.IsError )
			return;

		_weaponGo = new GameObject( GameObject, true, "WeaponWorld" );
		var renderer = _weaponGo.Components.Create<SkinnedModelRenderer>();
		renderer.UseAnimGraph = false;
		renderer.Model = model;
		renderer.Tint = Color.White;
	}

	protected override void OnUpdate()
	{
		if ( !_anim.IsValid() )
			return;

		var dt = MathF.Max( Time.Delta, 0.0001f );
		var pos = GameObject.WorldPosition;
		if ( !_hasLastPos )
		{
			_lastPos = pos;
			_hasLastPos = true;
		}

		var vel = (pos - _lastPos) / dt;
		_lastPos = pos;

		_anim.WithVelocity( vel );
		_anim.IsGrounded = true;
		_anim.DuckLevel = 0f;
		_anim.AimAngle = GameObject.WorldRotation;
		_anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
	}

	protected override void OnPreRender() => AttachWeaponToHand();

	private void AttachWeaponToHand()
	{
		if ( _handAttached || !_weaponGo.IsValid() || !_skin.IsValid() || !_skin.Model.IsValid() )
			return;

		_skin.CreateBoneObjects = true;

		foreach ( var boneName in RightHandBones )
		{
			if ( !_skin.Model.Bones.HasBone( boneName ) )
				continue;

			var bone = _skin.GetBoneObject( _skin.Model.Bones.GetBone( boneName ) );
			if ( !bone.IsValid() )
				continue;

			_weaponGo.SetParent( bone );
			_weaponGo.LocalRotation = Rotation.From( GunHandEuler );
			_weaponGo.LocalPosition = GunHandOffset;
			_weaponGo.LocalScale = Vector3.One;
			_handAttached = true;
			return;
		}
	}
}
