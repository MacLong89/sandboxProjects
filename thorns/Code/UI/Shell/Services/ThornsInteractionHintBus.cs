namespace Sandbox;

/// <summary>Interaction hint ownership — prompts from proximity, taming, storage, etc.</summary>
public sealed class ThornsInteractionHintBus : IThornsInteractionHintBus
{
	IThornsHudPresenter _presenter;

	public string Message { get; private set; } = "";
	public bool HasWorldAnchor { get; private set; }
	public Vector3 WorldAnchor { get; private set; }
	public GameObject WorldTarget { get; private set; }

	public void BindPresenter( IThornsHudPresenter presenter ) => _presenter = presenter;

	public void Set( string message, GameObject target, Vector3 fallbackWorldAnchor, bool hasWorldAnchor )
	{
		if ( _presenter is not { IsLocalOwned: true } )
			return;

		Message = ThornsInteractionPromptText.Format( message ?? "" );
		HasWorldAnchor = hasWorldAnchor && !string.IsNullOrWhiteSpace( Message );
		WorldAnchor = fallbackWorldAnchor;
		WorldTarget = target;
		_presenter.OnInteractionHintChanged();
	}

	public void Clear() => Set( "", null, default, false );

	public bool TryProjectAnchor( Component projectionHost, out Vector2 screenPos )
	{
		screenPos = default;
		if ( !HasWorldAnchor || string.IsNullOrWhiteSpace( Message ) )
			return false;

		return ThornsInteractionHintProjection.TryProject(
			projectionHost,
			WorldTarget,
			WorldAnchor,
			out screenPos );
	}
}

/// <summary>Screen projection for world-anchored interaction hints.</summary>
public static class ThornsInteractionHintProjection
{
	public static bool TryProject( Component host, GameObject target, Vector3 fallbackWorldAnchor, out Vector2 screenPos )
	{
		screenPos = default;
		var cam = host?.Components.GetInDescendantsOrSelf<CameraComponent>( true );
		if ( !cam.IsValid() || !cam.Enabled )
		{
			var scene = host?.Scene;
			if ( scene is not null && scene.IsValid() )
			{
				foreach ( var c in scene.GetAllComponents<CameraComponent>() )
				{
					if ( c.IsValid() && c.Enabled && c.IsMainCamera )
					{
						cam = c;
						break;
					}
				}
			}
		}

		if ( !cam.IsValid() || !cam.Enabled )
			return false;

		var bounds = default( BBox );
		var hasBounds = target.IsValid() && TryGetGameplayHintWorldBounds( target, out bounds );
		var anchor = hasBounds ? bounds.Center : fallbackWorldAnchor;
		var toAnchor = anchor - cam.GameObject.WorldPosition;
		if ( Vector3.Dot( cam.GameObject.WorldRotation.Forward, toAnchor ) <= 1f )
			return false;

		var bb = new BBox( anchor - Vector3.One, anchor + Vector3.One );
		var rect = cam.BBoxToScreenPixels( bb, out var anyVisible );
		if ( !anyVisible )
			return false;

		screenPos = rect.Center;
		return screenPos.x >= -256f && screenPos.x <= Screen.Width + 256f
		       && screenPos.y >= -256f && screenPos.y <= Screen.Height + 256f;
	}

	static bool TryGetGameplayHintWorldBounds( GameObject target, out BBox bounds )
	{
		bounds = default;
		if ( !target.IsValid() )
			return false;

		var any = false;
		foreach ( var col in target.Components.GetAll<Collider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( !col.IsValid() || !col.Enabled || col.IsTrigger )
				continue;

			if ( !TryGetColliderWorldBounds( col, out var bb ) )
				continue;

			UnionBounds( ref bounds, ref any, bb );
		}

		if ( any )
			return true;

		foreach ( var mr in target.Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( !mr.IsValid() || !mr.Enabled || !mr.Model.IsValid() )
				continue;

			var bb = TransformBoundsToWorld( mr.WorldTransform, mr.Model.Bounds );
			UnionBounds( ref bounds, ref any, bb );
		}

		foreach ( var sk in target.Components.GetAll<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( !sk.IsValid() || !sk.Enabled || !sk.Model.IsValid() )
				continue;

			var bb = TransformBoundsToWorld( sk.WorldTransform, sk.Model.Bounds );
			UnionBounds( ref bounds, ref any, bb );
		}

		return any;
	}

	static bool TryGetColliderWorldBounds( Collider col, out BBox worldBounds )
	{
		worldBounds = default;
		if ( !col.IsValid() )
			return false;

		if ( col is BoxCollider bc )
		{
			worldBounds = TransformBoundsToWorld( bc.WorldTransform, new BBox( bc.Center - bc.Scale * 0.5f, bc.Center + bc.Scale * 0.5f ) );
			return worldBounds.Size.LengthSquared > 1e-8f;
		}

		if ( col is ModelCollider mc && mc.Model.IsValid() )
		{
			worldBounds = TransformBoundsToWorld( mc.WorldTransform, mc.Model.Bounds );
			return worldBounds.Size.LengthSquared > 1e-8f;
		}

		if ( col is CapsuleCollider cap )
		{
			var wt = cap.WorldTransform;
			var a = wt.PointToWorld( cap.Start );
			var b = wt.PointToWorld( cap.End );
			var r = MathF.Max( 0.05f, cap.Radius * MathF.Max( wt.Scale.x, MathF.Max( wt.Scale.y, wt.Scale.z ) ) );
			worldBounds = new BBox(
				Vector3.Min( a, b ) - new Vector3( r, r, r ),
				Vector3.Max( a, b ) + new Vector3( r, r, r ) );
			return true;
		}

		if ( col is SphereCollider sph )
		{
			var c = sph.WorldTransform.PointToWorld( sph.Center );
			var r = MathF.Max( 0.05f, sph.Radius * sph.WorldTransform.Scale.x );
			worldBounds = new BBox( c - new Vector3( r, r, r ), c + new Vector3( r, r, r ) );
			return true;
		}

		try
		{
			var local = col.LocalBounds;
			if ( local.Size.LengthSquared > 1e-8f )
			{
				worldBounds = TransformBoundsToWorld( col.WorldTransform, local );
				return true;
			}
		}
		catch
		{
			// Some collider types don't expose LocalBounds.
		}

		return false;
	}

	static void UnionBounds( ref BBox bounds, ref bool any, BBox next )
	{
		if ( next.Size.LengthSquared <= 1e-8f )
			return;

		if ( !any )
		{
			bounds = next;
			any = true;
			return;
		}

		bounds = new BBox( Vector3.Min( bounds.Mins, next.Mins ), Vector3.Max( bounds.Maxs, next.Maxs ) );
	}

	static BBox TransformBoundsToWorld( Transform wt, BBox local )
	{
		Span<Vector3> corners = stackalloc Vector3[8];
		var c = local.Center;
		var e = local.Size * 0.5f;
		var i = 0;
		for ( var sx = -1; sx <= 1; sx += 2 )
		for ( var sy = -1; sy <= 1; sy += 2 )
		for ( var sz = -1; sz <= 1; sz += 2 )
			corners[i++] = wt.PointToWorld( c + new Vector3( sx * e.x, sy * e.y, sz * e.z ) );

		var mn = corners[0];
		var mx = corners[0];
		for ( var k = 1; k < 8; k++ )
		{
			mn = Vector3.Min( mn, corners[k] );
			mx = Vector3.Max( mx, corners[k] );
		}

		return new BBox( mn, mx );
	}
}
