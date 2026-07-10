namespace Sandbox;

/// <summary>
/// Local view-distance visibility for resource meshes: toggles <see cref="NearRenderer"/> by distance.
/// Optional <see cref="FarRenderer"/> when set (legacy); otherwise nothing is drawn past <see cref="HideNearDistance"/>.
/// </summary>
public sealed class ThornsResourceLodProxy : Component
{
	[Property] public ModelRenderer NearRenderer { get; set; }
	[Property] public ModelRenderer FarRenderer { get; set; }
	[Property] public float HideNearDistance { get; set; } = 9000f;
	[Property] public float ShowNearDistance { get; set; } = 7600f;
	[Property] public float UpdateIntervalSeconds { get; set; } = 0.38f;

	double _nextTick;
	bool _nearVisible;
	ThornsResourceNode _node;

	/// <summary>When false, solid hulls stay off while the near mesh is distance-culled.</summary>
	public bool CollidersMatchNearMesh => _nearVisible;

	/// <summary>LOD updates were skipped while <see cref="ThornsResourceNode.IsDepleted"/> — resample distance when the node respawns.</summary>
	bool _lodHeldForDepletion;

	bool AttachedResourceDepleted()
	{
		var node = ResolveAttachedResourceNode();
		return node.IsValid() && node.IsDepleted;
	}

	ThornsResourceNode ResolveAttachedResourceNode()
	{
		if ( _node.IsValid() )
			return _node;

		_node = GameObject.Components.Get<ThornsResourceNode>( FindMode.EnabledInSelf );
		return _node;
	}

	void ResyncNearVisibilityFromViewerDistance()
	{
		if ( !TryGetLocalViewerPosition( out var viewerPos ) )
		{
			// Pawn may not exist yet when deferred mineral spawns drain — stay visible until distance can be measured.
			_nearVisible = true;
			ApplyState();
			return;
		}

		_nearVisible = false;
		var hide = Math.Max( 1f, HideNearDistance );
		var hideSq = hide * hide;
		var d2 = (GameObject.WorldPosition - viewerPos).LengthSquared;
		_nearVisible = d2 <= hideSq;

		ApplyState();
	}

	protected override void OnStart()
	{
		if ( AttachedResourceDepleted() )
		{
			if ( NearRenderer.IsValid() )
				NearRenderer.Enabled = false;
			if ( FarRenderer.IsValid() )
				FarRenderer.Enabled = false;
			_lodHeldForDepletion = true;
			return;
		}

		_lodHeldForDepletion = false;
		ResyncNearVisibilityFromViewerDistance();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( AttachedResourceDepleted() )
		{
			if ( NearRenderer.IsValid() )
				NearRenderer.Enabled = false;
			if ( FarRenderer.IsValid() )
				FarRenderer.Enabled = false;
			_lodHeldForDepletion = true;
			return;
		}

		if ( _lodHeldForDepletion )
		{
			_lodHeldForDepletion = false;
			ResyncNearVisibilityFromViewerDistance();
		}

		if ( Time.Now < _nextTick )
			return;

		_nextTick = Time.Now + Math.Max( 0.05f, UpdateIntervalSeconds );

		if ( !TryGetLocalViewerPosition( out var viewerPos ) )
		{
			if ( !_nearVisible )
			{
				_nearVisible = true;
				ApplyState();
			}

			return;
		}

		var hide = Math.Max( 1f, HideNearDistance );
		var show = Math.Min( hide, Math.Max( 1f, ShowNearDistance ) );
		var d2 = (GameObject.WorldPosition - viewerPos).LengthSquared;
		var hideSq = hide * hide;
		var showSq = show * show;

		if ( _nearVisible )
		{
			if ( d2 > hideSq )
			{
				_nearVisible = false;
				ApplyState();
			}
		}
		else
		{
			if ( d2 < showSq )
			{
				_nearVisible = true;
				ApplyState();
			}
		}
	}

	void ApplyState()
	{
		if ( NearRenderer.IsValid() )
			NearRenderer.Enabled = _nearVisible;
		if ( FarRenderer.IsValid() )
			FarRenderer.Enabled = !_nearVisible;

		SyncSolidCollidersToLodVisibility();
	}

	/// <summary>
	/// Stone / ore LOD hides the mesh past <see cref="HideNearDistance"/> but the root hull stayed enabled — invisible walk blockers.
	/// Depleted nodes disable colliders in <see cref="ThornsResourceNode.UpdateVisualFromState"/>.
	/// </summary>
	void SyncSolidCollidersToLodVisibility()
	{
		var node = ResolveAttachedResourceNode();
		if ( node.IsValid() && node.IsDepleted )
			return;

		var solid = _nearVisible;

		foreach ( var bc in GameObject.Components.GetAll<BoxCollider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( bc.IsValid() && !bc.IsTrigger )
				bc.Enabled = solid;
		}

		foreach ( var mc in GameObject.Components.GetAll<ModelCollider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( mc.IsValid() && !mc.IsTrigger )
				mc.Enabled = solid;
		}
	}

	bool TryGetLocalViewerPosition( out Vector3 pos ) => ThornsLocalViewer.TryGetWorldPosition( out pos );
}
