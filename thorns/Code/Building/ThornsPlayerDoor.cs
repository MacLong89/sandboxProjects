namespace Sandbox;

/// <summary>
/// Hinged door panel on a player-placed <c>wood_doorframe</c> — owner-only <see cref="ThornsInputInteract"/> toggle, 90° swing.
/// </summary>
[Title( "Thorns — Player door" )]
[Category( "Thorns/Building" )]
[Icon( "door_front" )]
[Order( 41 )]
public sealed class ThornsPlayerDoor : Component
{
	public const float InteractionRange = ThornsBuildingVisuals.PlaceableInteractionUseRange;
	public const float OpenAngleDegrees = 90f;
	public const float SwingSeconds = 0.32f;

	public static readonly Dictionary<Guid, ThornsPlayerDoor> ActiveByFrameId = new();

	static Vector3 HingeLocal => ThornsBuildingModule.DoorPanelHingeLocal;

	static Vector3 PanelOffsetFromHinge => ThornsBuildingModule.DoorPanelOffsetFromHinge;

	[Sync( SyncFlags.FromHost )] public bool DoorOpenSync { get; set; }

	ThornsPlacedStructure _frame;
	GameObject _hingeGo;
	GameObject _panelGo;
	ModelRenderer _panelMr;
	ModelCollider _panelCollider;
	float _displayAngleDeg;
	float _animFromDeg;
	float _animToDeg;
	double _animStartTime = -1;
	int _lastVisualTier = -1;

	public Guid FrameInstanceId => _frame.IsValid() ? _frame.InstanceId : Guid.Empty;

	protected override void OnAwake()
	{
		_frame = Components.Get<ThornsPlacedStructure>();
		if ( _frame.IsValid() && _frame.InstanceId != Guid.Empty )
			ActiveByFrameId[_frame.InstanceId] = this;
	}

	protected override void OnDestroy()
	{
		var id = FrameInstanceId;
		if ( id != Guid.Empty )
			ActiveByFrameId.Remove( id );
	}

	protected override void OnStart()
	{
		_frame = Components.Get<ThornsPlacedStructure>();
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

	public static void HostEnsureOnDoorframe( ThornsPlacedStructure frame, bool doorOpen = false )
	{
		if ( !Networking.IsHost || frame is null || !frame.IsValid() )
			return;

		if ( !string.Equals( frame.StructureDefId, "wood_doorframe", StringComparison.OrdinalIgnoreCase ) )
			return;

		var door = frame.Components.Get<ThornsPlayerDoor>();
		if ( !door.IsValid() )
			door = frame.Components.Create<ThornsPlayerDoor>();

		door._frame = frame;
		door.EnsureVisualHierarchy();
		door.RefreshPanelTier( frame.MaterialTier );
		door.HostSetOpenImmediate( doorOpen );
	}

	public static bool TryFindBestUnderAim( GameObject pawnRoot, float maxDist, out ThornsPlayerDoor best )
	{
		best = default;
		ThornsPlayerDoor pick = default;
		var bestD = float.PositiveInfinity;

		foreach ( var door in ActiveByFrameId.Values )
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

	public void RequestToggleFromLocalOwner()
	{
		if ( Connection.Local is null )
			return;

		RequestToggleDoor();
	}

	[Rpc.Host]
	void RequestToggleDoor()
	{
		if ( !Networking.IsHost )
			return;

		HostTryToggle( Rpc.Caller );
	}

	void HostTryToggle( Connection caller )
	{
		if ( !Networking.IsHost || caller is null || !_frame.IsValid() )
			return;

		if ( !HostCallerIsPlacer( caller ) )
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

		if ( !ThornsPawnConnectionIndex.TryGetByOwnerId( opener.Id, out var pawn ) || !pawn.IsValid() )
			return;

		pawn.Components.Get<ThornsInventory>()?.HostNotifyOpenBuildSfx( GameObject.WorldPosition );
	}

	public void HostSetOpenImmediate( bool open )
	{
		DoorOpenSync = open;
		_displayAngleDeg = open ? OpenAngleDegrees : 0f;
		_animStartTime = -1;
		ApplyDisplayAngle( _displayAngleDeg );
	}

	bool HostCallerIsPlacer( Connection caller )
	{
		if ( caller is null || !_frame.IsValid() )
			return false;

		if ( _frame.OwnerConnectionId != Guid.Empty && caller.Id == _frame.OwnerConnectionId )
			return true;

		var key = ThornsPersistenceIdentity.GetStableAccountKey( caller );
		return !string.IsNullOrWhiteSpace( key )
		       && string.Equals( key, _frame.OwnerAccountKeySync ?? "", StringComparison.Ordinal );
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
		_panelGo.LocalScale = ThornsBuildingVisuals.StructureLocalScale( "wood_door" );

		_panelMr = _panelGo.Components.Create<ModelRenderer>();
		_panelMr.MaterialOverride = default;
		_panelMr.Tint = Color.White;

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

		var model = ThornsBuildingVisuals.StructureModel( "wood_door", materialTier );
		_panelMr.Model = model;
		_panelMr.MaterialOverride = default;
		_panelMr.Tint = Color.White;
		if ( _panelCollider.IsValid() )
			_panelCollider.Model = model;
	}

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
		_ = scene;
		if ( c is null )
			return default;

		return ThornsPawnConnectionIndex.TryGetPawnGameObject( c, out var root ) ? root : default;
	}
}
