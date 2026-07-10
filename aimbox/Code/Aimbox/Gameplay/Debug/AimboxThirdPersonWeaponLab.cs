namespace Sandbox;

/// <summary>
/// Third-person world-weapon tuning lab — pair with <see cref="AimboxTpWeaponLabDummy"/> in <c>tp_weapon_lab.scene</c>.
/// Press Play, select this object, swap weapon and edit transform fields live in the inspector.
/// </summary>
[Title( "Aimbox Third Person Weapon Lab" )]
[Category( "Aimbox/Debug" )]
[Icon( "accessibility" )]
public sealed class AimboxThirdPersonWeaponLab : Component, Component.ExecuteInEditor
{
	public static AimboxThirdPersonWeaponLab Instance { get; private set; }

	[Property, Group( "Weapon" )] public AimboxWeaponId Weapon { get; set; }

	[Property, Group( "Hand Attach" ), Title( "Local position (+X fwd, +Y center, −Z down)" )]
	public Vector3 WeaponLocalPosition { get; set; }

	[Property, Group( "Hand Attach" )] public Angles WeaponLocalRotation { get; set; }

	[Property, Group( "Hand Attach" )] public Vector3 WeaponLocalScale { get; set; }

	[Property, Group( "Hand Attach" ), Title( "Bone name (empty = auto)" )]
	public string PreferredParentBone { get; set; } = "hand_R_IK_target";

	[Property, Group( "Body Fallback" )] public bool UseBodyFallback { get; set; } = false;

	[Property, Group( "Body Fallback" )]
	public Vector3 BodyFallbackLocalPosition { get; set; }

	[Property, Group( "Pose" )] public bool PreviewCrouch { get; set; } = false;

	[Property, ReadOnly, Group( "Status" )] public string DummyStatus { get; private set; } = "Waiting for dummy…";

	[Property, ReadOnly, Group( "Status" )] public string AttachStatus { get; private set; } = "";

	[Property, ReadOnly, Group( "When Done" ), Title( "Paste into AimboxCitizenPresentation.cs" )]
	public string ProductionCopyPaste { get; private set; } = "";

	AimboxTpWeaponLabDummy _dummy;
	int _lastRevision = -1;

	public void PullProductionDefaults()
	{
		WeaponLocalPosition = AimboxCitizenPresentation.WorldWeaponHandLocalPosition;
		WeaponLocalRotation = AimboxCitizenPresentation.WorldWeaponHandLocalEulerDegrees;
		WeaponLocalScale = AimboxCitizenPresentation.WorldWeaponLocalScale;
		BodyFallbackLocalPosition = AimboxCitizenPresentation.WorldWeaponLocalPositionRelBody;
		PreferredParentBone = "hand_R_IK_target";
		RefreshStatus();
	}

	public void NotifyAttach( string message )
	{
		if ( AttachStatus != message )
			AttachStatus = message;
	}

	protected override void OnStart()
	{
		Instance = this;
		Weapon = AimboxWeaponId.M4A1;
		WeaponLocalPosition = AimboxCitizenPresentation.WorldWeaponHandLocalPosition;
		WeaponLocalRotation = AimboxCitizenPresentation.WorldWeaponHandLocalEulerDegrees;
		WeaponLocalScale = AimboxCitizenPresentation.WorldWeaponLocalScale;
		BodyFallbackLocalPosition = AimboxCitizenPresentation.WorldWeaponLocalPositionRelBody;
		_dummy = GameObject.Components.GetAll<AimboxTpWeaponLabDummy>( FindMode.EverythingInSelfAndDescendants ).FirstOrDefault()
		          ?? Scene.GetAllComponents<AimboxTpWeaponLabDummy>().FirstOrDefault();
		RefreshStatus();
	}

	protected override void OnEnabled() => Instance = this;

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		_dummy ??= GameObject.Components.GetAll<AimboxTpWeaponLabDummy>( FindMode.EverythingInSelfAndDescendants ).FirstOrDefault()
		          ?? Scene.GetAllComponents<AimboxTpWeaponLabDummy>().FirstOrDefault();

		var revision = ComputeRevision();
		if ( revision != _lastRevision )
		{
			_lastRevision = revision;
			RefreshCopyPaste();
		}

		RefreshStatus();
	}

	void RefreshStatus()
	{
		var status = _dummy is not null && _dummy.IsValid()
			? $"Dummy '{_dummy.GameObject.Name}' ready."
			: "Add an AimboxTpWeaponLabDummy under this object.";
		if ( DummyStatus != status )
			DummyStatus = status;
	}

	void RefreshCopyPaste()
	{
		var p = WeaponLocalPosition;
		var r = WeaponLocalRotation;
		var s = WeaponLocalScale;
		var body = BodyFallbackLocalPosition;
		ProductionCopyPaste =
			$"public static readonly Vector3 WorldWeaponHandLocalPosition = new( {p.x}f, {p.y}f, {p.z}f );\n" +
			$"public static readonly Angles WorldWeaponHandLocalEulerDegrees = new( {r.pitch}f, {r.yaw}f, {r.roll}f );\n" +
			$"public static readonly Vector3 WorldWeaponLocalScale = new( {s.x}f, {s.y}f, {s.z}f );\n" +
			$"public static readonly Vector3 WorldWeaponLocalPositionRelBody = new( {body.x}f, {body.y}f, {body.z}f );";
	}

	int ComputeRevision()
	{
		var hash = new HashCode();
		hash.Add( Weapon );
		hash.Add( WeaponLocalPosition );
		hash.Add( WeaponLocalRotation );
		hash.Add( WeaponLocalScale );
		hash.Add( PreferredParentBone );
		hash.Add( UseBodyFallback );
		hash.Add( BodyFallbackLocalPosition );
		hash.Add( PreviewCrouch );
		return hash.ToHashCode();
	}
}
