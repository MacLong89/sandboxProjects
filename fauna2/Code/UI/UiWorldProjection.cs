namespace Fauna2.UI;

/// <summary>Project world points onto HUD overlay panels tied to the game camera.</summary>
public static class UiWorldProjection
{
	public static CameraComponent GetUiCamera( Scene scene ) => ResolveGameCamera( scene );

	public static void BindScreenPanelCamera( Scene scene, CameraComponent camera )
	{
		if ( !camera.IsValid() )
			return;

		foreach ( var screenPanel in scene.GetAllComponents<ScreenPanel>() )
			screenPanel.TargetCamera = camera;
	}

	/// <summary>World position → 0–1 coordinates within the target overlay panel (top-left origin).</summary>
	public static bool TryWorldToOverlay( Scene scene, Sandbox.UI.Panel panel, Vector3 worldPosition, out Vector2 normalized )
	{
		normalized = default;

		if ( panel is null || !panel.IsValid )
			return false;

		var camera = ResolveGameCamera( scene );
		if ( !camera.IsValid() )
			return false;

		if ( !TryWorldToViewport( camera, worldPosition, out var viewportNormal ) )
			return false;

		var screenPx = new Vector2( viewportNormal.x * Screen.Width, viewportNormal.y * Screen.Height );
		normalized = panel.ScreenPositionToPanelDelta( screenPx );
		return true;
	}

	/// <summary>Viewport-normalized 0–1 (top-left) aligned with <see cref="CameraComponent.ScreenPixelToRay"/>.</summary>
	public static bool TryWorldToViewport( CameraComponent camera, Vector3 worldPosition, out Vector2 viewportNormal )
	{
		viewportNormal = default;
		if ( !camera.IsValid() )
			return false;

		if ( !TryProjectOrthoViewport( worldPosition, out viewportNormal ) )
			viewportNormal = camera.PointToScreenNormal( worldPosition, out _ );

		RefineViewportToWorld( camera, worldPosition, ref viewportNormal );

		if ( !TryGetRayGroundError( camera, viewportNormal, worldPosition, out var error ) )
			return false;

		return error <= 96f;
	}

	/// <summary>Initial ortho guess using the same follow rig as <see cref="ZooCameraController"/>.</summary>
	public static bool TryProjectOrthoViewport( Vector3 worldPosition, out Vector2 viewportNormal )
	{
		viewportNormal = default;

		var controller = ZooCameraController.Instance;
		if ( !controller.IsValid() )
			return false;

		var camera = controller.Components.Get<CameraComponent>();
		if ( !camera.IsValid() || !camera.Orthographic )
			return false;

		var focus = controller.FocusPoint;
		var rot = camera.GameObject.WorldRotation;
		var delta = worldPosition - focus;

		var localX = Vector3.Dot( delta, rot.Right );
		var localY = Vector3.Dot( delta, rot.Up );

		var orthoH = camera.OrthographicHeight;
		if ( orthoH <= 1f )
			return false;

		var aspect = Screen.Aspect;
		if ( aspect <= 0.01f )
			aspect = 16f / 9f;

		var orthoW = orthoH * aspect;
		viewportNormal = new Vector2(
			0.5f + localX / (orthoW * 2f),
			0.5f - localY / (orthoH * 2f ) );

		return true;
	}

	static void RefineViewportToWorld( CameraComponent camera, Vector3 worldPosition, ref Vector2 viewportNormal )
	{
		var orthoH = camera.OrthographicHeight;
		if ( orthoH <= 1f )
			return;

		var aspect = Screen.Aspect;
		if ( aspect <= 0.01f )
			aspect = 16f / 9f;

		var orthoW = orthoH * aspect;

		for ( var i = 0; i < 8; i++ )
		{
			if ( !TryGetRayGroundError( camera, viewportNormal, worldPosition, out var error, out var errX, out var errY ) )
				return;

			if ( error <= 2f )
				return;

			viewportNormal.x += errX / (orthoW * 2f);
			viewportNormal.y -= errY / (orthoH * 2f);
			viewportNormal.x = viewportNormal.x.Clamp( -0.25f, 1.25f );
			viewportNormal.y = viewportNormal.y.Clamp( -0.25f, 1.25f );
		}
	}

	static bool TryGetRayGroundError(
		CameraComponent camera,
		Vector2 viewportNormal,
		Vector3 worldPosition,
		out float error ) =>
		TryGetRayGroundError( camera, viewportNormal, worldPosition, out error, out _, out _ );

	static bool TryGetRayGroundError(
		CameraComponent camera,
		Vector2 viewportNormal,
		Vector3 worldPosition,
		out float error,
		out float errX,
		out float errY )
	{
		error = float.MaxValue;
		errX = 0f;
		errY = 0f;

		if ( !camera.IsValid() )
			return false;

		var screenPx = new Vector2( viewportNormal.x * Screen.Width, viewportNormal.y * Screen.Height );
		var ray = camera.ScreenPixelToRay( screenPx );
		if ( MathF.Abs( ray.Forward.z ) < 0.0001f )
			return false;

		var t = (worldPosition.z - ray.Position.z) / ray.Forward.z;
		if ( t < 0f )
			return false;

		var hit = ray.Project( t );
		errX = worldPosition.x - hit.x;
		errY = worldPosition.y - hit.y;
		error = MathF.Sqrt( errX * errX + errY * errY );
		return true;
	}

	static CameraComponent ResolveGameCamera( Scene scene )
	{
		if ( ZooCameraController.Instance.IsValid() )
		{
			var camera = ZooCameraController.Instance.Components.Get<CameraComponent>();
			if ( camera.IsValid() )
				return camera;
		}

		if ( scene.Camera.IsValid() )
			return scene.Camera;

		foreach ( var screenPanel in scene.GetAllComponents<ScreenPanel>() )
		{
			if ( screenPanel.TargetCamera.IsValid() )
				return screenPanel.TargetCamera;
		}

		return null;
	}
}
