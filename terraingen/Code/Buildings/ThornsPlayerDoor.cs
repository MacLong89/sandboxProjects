namespace Terraingen.Buildings;

using Sandbox.Network;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.Rendering;
using Terraingen.World;

/// <summary>
/// Hinged door panel on a player-placed <c>wood_doorframe</c> — owner-only Use toggle, 90° swing.
/// </summary>
[Title( "Thorns Player Door" )]
[Category( "Buildings" )]
[Icon( "door_front" )]
public sealed class ThornsPlayerDoor : Component
{
	public const float InteractionRange = 128f;
	public const float OpenAngleDegrees = 90f;
	public const float SwingSeconds = 0.32f;

	public static readonly Dictionary<string, ThornsPlayerDoor> ActiveByFrameKey = new( StringComparer.OrdinalIgnoreCase );

	static Vector3 HingeLocal => ThornsBuildingModule.DoorPanelHingeLocal;

	static Vector3 PanelOffsetFromHinge => ThornsBuildingModule.DoorPanelOffsetFromHinge;

	[Sync( SyncFlags.FromHost )] public bool DoorOpenSync { get; set; }

	ThornsPlacedBuildStructure _frame;
	GameObject _hingeGo;
	GameObject _panelGo;
	ModelRenderer _panelMr;
	ModelCollider _panelCollider;
	float _displayAngleDeg;
	float _animFromDeg;
	float _animToDeg;
	double _animStartTime = -1;
	int _lastVisualTier = -1;

	public string FrameInstanceKey => _frame.IsValid() ? _frame.InstanceKey ?? "" : "";

	protected override void OnAwake()
	{
		_frame = Components.Get<ThornsPlacedBuildStructure>();
		RegisterActive();
	}

	protected override void OnDestroy()
	{
		var key = FrameInstanceKey;
		if ( !string.IsNullOrWhiteSpace( key ) )
			ActiveByFrameKey.Remove( key );
	}

	protected override void OnStart()
	{
		_frame = Components.Get<ThornsPlacedBuildStructure>();
		RegisterActive();
		EnsureVisualHierarchy();
		RefreshPanelTier( _frame.IsValid() ? _frame.MaterialTier : 0 );
		_displayAngleDeg = DoorOpenSync ? OpenAngleDegrees : 0f;
		ApplyDisplayAngle( _displayAngleDeg );
	}

	protected override void OnUpdate()
	{
		if ( !_hingeGo.IsValid() )
			EnsureVisualHierarchy();

		if ( _frame.IsValid() && _frame.MaterialTier != _lastVisualTier )
			RefreshPanelTier( _frame.MaterialTier );

		TickSwingAnimation();
	}

	void RegisterActive()
	{
		if ( !_frame.IsValid() || string.IsNullOrWhiteSpace( _frame.InstanceKey ) )
			return;

		ActiveByFrameKey[_frame.InstanceKey] = this;
	}

	public static void HostEnsureOnDoorframe( ThornsPlacedBuildStructure frame, bool doorOpen = false )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || frame is null || !frame.IsValid() )
			return;

		if ( !string.Equals( frame.StructureId, "wood_doorframe", StringComparison.OrdinalIgnoreCase ) )
			return;

		var door = frame.Components.Get<ThornsPlayerDoor>();
		if ( !door.IsValid() )
			door = frame.Components.Create<ThornsPlayerDoor>();

		door._frame = frame;
		door.RegisterActive();
		door.EnsureVisualHierarchy();
		door.RefreshPanelTier( frame.MaterialTier );
		door.HostSetOpenImmediate( doorOpen );
	}

	public static bool TryFindBestUnderAim( GameObject pawnRoot, float maxDist, out ThornsPlayerDoor best )
	{
		best = default;
		ThornsPlayerDoor pick = default;
		var bestD = float.PositiveInfinity;

		foreach ( var door in ActiveByFrameKey.Values )
		{
			if ( !door.IsValid() || !door._frame.IsValid() )
				continue;

			var root = door.GameObject;
			var d = (root.WorldPosition - pawnRoot.WorldPosition).Length;
			if ( d > maxDist || d >= bestD )
				continue;

			if ( !ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, root, maxDist ) )
				continue;

			bestD = d;
			pick = door;
		}

		best = pick;
		return best.IsValid();
	}

	public static bool TryFindBestUnderAimForOwner( GameObject pawnRoot, float maxDist, out ThornsPlayerDoor best )
	{
		best = default;
		if ( !TryFindBestUnderAim( pawnRoot, maxDist, out var door ) || !door.IsValid() )
			return false;

		if ( !door.LocalPawnIsOwner( pawnRoot ) )
			return false;

		best = door;
		return true;
	}

	public void RequestToggleFromLocalOwner()
	{
		if ( Connection.Local is null )
			return;

		RequestToggleDoor();
	}

	[Rpc.Host]
	void RequestToggleDoor()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		HostTryToggle( Rpc.Caller );
	}

	void HostTryToggle( Connection caller )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || caller is null || !_frame.IsValid() )
			return;

		if ( !HostCallerIsOwner( caller ) )
			return;

		if ( !HostValidateCallerInRange( caller ) )
			return;

		HostBeginToggle( !DoorOpenSync, caller );
	}

	void HostBeginToggle( bool open, Connection opener )
	{
		_animFromDeg = _displayAngleDeg;
		_animToDeg = open ? OpenAngleDegrees : 0f;
		_animStartTime = Time.Now;
		DoorOpenSync = open;

		if ( !open || opener is null )
			return;

		var pawn = FindConnectionPawnRoot( GameObject.Scene, opener );
		if ( !pawn.IsValid() )
			return;

		ThornsGameplaySfx.PlayNetworkedWorldInteraction( GameObject.WorldPosition, ThornsGameplaySfx.OpenBuild );
	}

	public void HostSetOpenImmediate( bool open )
	{
		DoorOpenSync = open;
		_displayAngleDeg = open ? OpenAngleDegrees : 0f;
		_animStartTime = -1;
		ApplyDisplayAngle( _displayAngleDeg );
	}

	bool LocalPawnIsOwner( GameObject pawnRoot )
	{
		if ( !_frame.IsValid() || pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var ownerKey = _frame.OwnerAccountKey ?? "";
		if ( string.IsNullOrWhiteSpace( ownerKey ) )
			return true;

		var pawnKey = ThornsPersistenceIdentity.GetStableAccountKey( pawnRoot );
		return string.Equals( ownerKey, pawnKey, StringComparison.OrdinalIgnoreCase );
	}

	bool HostCallerIsOwner( Connection caller )
	{
		if ( caller is null || !_frame.IsValid() )
			return false;

		var ownerKey = _frame.OwnerAccountKey ?? "";
		if ( string.IsNullOrWhiteSpace( ownerKey ) )
			return true;

		var callerKey = ThornsPersistenceIdentity.GetStableAccountKey( caller );
		return string.Equals( ownerKey, callerKey, StringComparison.OrdinalIgnoreCase );
	}

	bool HostValidateCallerInRange( Connection caller )
	{
		var pawn = FindConnectionPawnRoot( GameObject.Scene, caller );
		if ( !pawn.IsValid() )
			return false;

		if ( (pawn.WorldPosition - GameObject.WorldPosition).Length > InteractionRange )
			return false;

		return ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawn, GameObject, InteractionRange );
	}

	void TickSwingAnimation()
	{
		if ( _animStartTime < 0 )
			return;

		var t = Math.Clamp( (float)( ( Time.Now - _animStartTime ) / SwingSeconds ), 0f, 1f );
		_displayAngleDeg = _animFromDeg + ( _animToDeg - _animFromDeg ) * t;
		ApplyDisplayAngle( _displayAngleDeg );

		if ( t >= 1f - 0.0001f )
			_animStartTime = -1;
	}

	void EnsureVisualHierarchy()
	{
		TryBindExistingVisualHierarchy();

		var created = !_hingeGo.IsValid() || !_panelGo.IsValid() || !_panelMr.IsValid();

		if ( !created )
		{
			ApplyDoorHierarchyLocalPose();
			return;
		}

		_hingeGo = new GameObject( true, "ThornsDoorHinge" );
		_hingeGo.SetParent( GameObject );
		_hingeGo.LocalPosition = HingeLocal;
		_hingeGo.LocalRotation = Rotation.Identity;
		_hingeGo.LocalScale = Vector3.One;

		_panelGo = new GameObject( true, "ThornsDoorPanel" );
		_panelGo.SetParent( _hingeGo );
		_panelGo.LocalPosition = PanelOffsetFromHinge;
		_panelGo.LocalRotation = Rotation.Identity;
		_panelGo.LocalScale = Vector3.One;

		_panelMr = _panelGo.Components.Create<ModelRenderer>();
		_panelMr.MaterialOverride = default;
		_panelMr.Tint = Color.White;
		ThornsWorldShadowUtil.EnableWorldShadows( _panelMr );

		_panelCollider = _panelGo.Components.Create<ModelCollider>();
		_panelCollider.IsTrigger = false;
		_panelCollider.Static = true;

		ApplyDoorHierarchyLocalPose();
	}

	void ApplyDoorHierarchyLocalPose()
	{
		if ( _hingeGo.IsValid() )
			_hingeGo.LocalPosition = HingeLocal;

		if ( _panelGo.IsValid() )
		{
			_panelGo.LocalPosition = PanelOffsetFromHinge;
			_panelGo.LocalRotation = Rotation.Identity;
		}
	}

	void TryBindExistingVisualHierarchy()
	{
		if ( !_hingeGo.IsValid() )
		{
			foreach ( var child in GameObject.Children )
			{
				if ( !child.IsValid() || child.Name != "ThornsDoorHinge" )
					continue;

				_hingeGo = child;
				break;
			}
		}

		if ( _hingeGo.IsValid() && !_panelGo.IsValid() )
		{
			foreach ( var child in _hingeGo.Children )
			{
				if ( !child.IsValid() || child.Name != "ThornsDoorPanel" )
					continue;

				_panelGo = child;
				break;
			}
		}

		if ( _panelGo.IsValid() )
		{
			if ( !_panelMr.IsValid() )
				_panelMr = _panelGo.Components.Get<ModelRenderer>();
			if ( !_panelCollider.IsValid() )
				_panelCollider = _panelGo.Components.Get<ModelCollider>();
		}
	}

	public void RefreshPanelTier( int materialTier )
	{
		_lastVisualTier = materialTier;
		if ( !_panelMr.IsValid() )
			return;

		var slug = FacadeMaterialSlug( materialTier );
		var model = ThornsBuildingWallMesh.GetDoorPanel( slug );
		_panelMr.Model = model;
		_panelMr.MaterialOverride = default;
		_panelMr.Tint = Color.White;
		if ( _panelCollider.IsValid() )
			_panelCollider.Model = model;
	}

	static string FacadeMaterialSlug( int materialTier ) =>
		Math.Clamp( materialTier, 0, ThornsPlacedBuildStructure.MaxMaterialTier ) switch
		{
			1 => "stone_brick",
			2 => "sheet_metal",
			_ => "barn_wood"
		};

	void ApplyDisplayAngle( float angleDeg )
	{
		if ( !_hingeGo.IsValid() )
			return;

		_hingeGo.LocalRotation = Rotation.FromAxis( Vector3.Up, angleDeg );

		if ( _panelCollider.IsValid() )
			_panelCollider.Enabled = angleDeg < 8f;
	}

	static GameObject FindConnectionPawnRoot( Scene scene, Connection c )
	{
		if ( c is null || scene is null || !scene.IsValid() )
			return default;

		var key = ThornsPersistenceIdentity.GetStableAccountKey( c );
		if ( string.IsNullOrWhiteSpace( key ) )
			return default;

		foreach ( var gameplay in scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( !gameplay.IsValid() )
				continue;

			if ( string.Equals( gameplay.AccountKey, key, StringComparison.OrdinalIgnoreCase ) )
				return gameplay.GameObject;
		}

		return default;
	}
}
