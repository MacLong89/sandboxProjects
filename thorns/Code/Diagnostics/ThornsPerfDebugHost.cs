using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Scene singleton — frame profiler tick + on-screen HUD when <see cref="ThornsPerfDebug.Enabled"/>.
/// </summary>
[Category( "Thorns/Diagnostics" )]
[Icon( "speed" )]
public sealed class ThornsPerfDebugHost : Component
{
	public static ThornsPerfDebugHost Instance { get; private set; }

	public static ThornsPerfDebugHost EnsureOn( GameObject host )
	{
		if ( host is null || !host.IsValid() )
			return default;

		var existing = host.Components.Get<ThornsPerfDebugHost>();
		if ( existing.IsValid() )
		{
			Instance = existing;
			EnsureFrameMarkers( host );
			return existing;
		}

		var created = host.Components.Create<ThornsPerfDebugHost>();
		Instance = created;
		EnsureFrameMarkers( host );
		return created;
	}

	static void EnsureFrameMarkers( GameObject host )
	{
		if ( !host.Components.Get<ThornsPerfDebugFrameBegin>().IsValid() )
			_ = host.Components.Create<ThornsPerfDebugFrameBegin>();
		if ( !host.Components.Get<ThornsPerfDebugFrameEnd>().IsValid() )
			_ = host.Components.Create<ThornsPerfDebugFrameEnd>();
	}

	protected override void OnStart()
	{
		Instance = this;
		ThornsPerfDebug.MarkLoadStarted();
		EnsureFrameMarkers( GameObject );
	}

	protected override void OnDestroy()
	{
		if ( ReferenceEquals( Instance, this ) )
			Instance = null;
	}
}

[Order( -10000 )]
sealed class ThornsPerfDebugFrameBegin : Component
{
	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		ThornsPerfDebug.BeginFrame();
	}
}

[Order( 10000 )]
sealed class ThornsPerfDebugFrameEnd : Component
{
	double _nextEntityRefresh;
	double _nextHudDraw;
	double _nextPlayableScan;
	CameraComponent _cachedMainCamera;
	double _nextMainCameraDiscoveryRealtime;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		ThornsPerfDebug.AccumulateFrame( Time.Delta );

		var now = Time.Now;
		if ( now >= _nextEntityRefresh )
		{
			_nextEntityRefresh = now + 1.0;
			ThornsPerfDebug.RefreshEntityEstimates( Scene );
		}

		TryMarkPlayable();
		DrawOverlayIfEnabled( now );
	}

	void TryMarkPlayable()
	{
		if ( ThornsPerfDebug.LoadPlayableMs >= 0 )
			return;

		if ( Time.Now < _nextPlayableScan )
			return;

		_nextPlayableScan = Time.Now + 0.5;

		foreach ( var pawn in Scene.GetAllComponents<ThornsPawn>() )
		{
			if ( pawn.IsValid() && pawn.IsLocal && !pawn.IsProxy )
			{
				ThornsPerfDebug.MarkPlayable();
				return;
			}
		}
	}

	void DrawOverlayIfEnabled( double now )
	{
		if ( !ThornsPerfDebug.Enabled || now < _nextHudDraw )
			return;

		_nextHudDraw = now + 0.1;

		CameraComponent camera = ResolveMainCamera();

		if ( camera is null || !camera.IsValid() )
			return;

		var hud = camera.Hud;
		var lines = ThornsPerfDebug.BuildOverlayLines();
		var y = 12f;
		const float lineHeight = 18f;
		const int fontSize = 13;

		foreach ( var line in lines )
		{
			hud.DrawText( new TextRendering.Scope( line, Color.White, fontSize ), new Vector2( 12f, y ) );
			y += lineHeight;
		}
	}

	CameraComponent ResolveMainCamera()
	{
		if ( _cachedMainCamera.IsValid() && _cachedMainCamera.Enabled && _cachedMainCamera.IsMainCamera )
			return _cachedMainCamera;

		if ( Time.Now < _nextMainCameraDiscoveryRealtime )
			return _cachedMainCamera;

		_nextMainCameraDiscoveryRealtime = Time.Now + 0.75;
		_cachedMainCamera = default;
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam.IsValid() && cam.Enabled && cam.IsMainCamera )
			{
				_cachedMainCamera = cam;
				break;
			}
		}

		return _cachedMainCamera;
	}
}
