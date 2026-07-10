namespace Sandbox;

public static class AimboxHitboxes
{
	public const float CitizenRadius = 18f;
	public const float CitizenFeetZ = 0f;
	/// <summary>Standing capsule top — slightly above citizen mesh hairline so grazing head shots register.</summary>
	public const float CitizenHeadTopZ = 78f;
	/// <summary>Hits at or above this local Z count as headshots (neck line slightly lower for a taller crit band).</summary>
	public const float CitizenHeadshotMinZ = 53f;
	/// <summary>Matches standing eye (64) − crouch eye (42).</summary>
	public const float CitizenCrouchHeightDrop = 22f;

	public static readonly Vector3 CitizenCapsuleStart = new( 0f, 0f, CitizenFeetZ + CitizenRadius );
	public static readonly Vector3 CitizenCapsuleEnd = new( 0f, 0f, CitizenHeadTopZ - CitizenRadius );

	public static float GetHeadTopZ( bool crouching ) =>
		CitizenHeadTopZ - (crouching ? CitizenCrouchHeightDrop : 0f);

	public static float GetHeadshotMinZ( bool crouching ) =>
		CitizenHeadshotMinZ - (crouching ? CitizenCrouchHeightDrop : 0f);

	public static Vector3 GetCapsuleStart( bool crouching ) =>
		new( 0f, 0f, CitizenFeetZ + CitizenRadius );

	public static Vector3 GetCapsuleEnd( bool crouching ) =>
		new( 0f, 0f, GetHeadTopZ( crouching ) - CitizenRadius );

	public static void ConfigureCitizenCapsule( CapsuleCollider collider ) =>
		ApplyCitizenHitbox( collider, false );

	public static void ApplyCitizenHitbox( CapsuleCollider collider, bool crouching )
	{
		if ( collider is null )
			return;

		collider.Start = GetCapsuleStart( crouching );
		collider.End = GetCapsuleEnd( crouching );
		collider.Radius = CitizenRadius;
		collider.IsTrigger = false;
	}

	public static bool IsHeadshot( Vector3 hitPosition, Vector3 rootPosition, bool crouching = false ) =>
		hitPosition.z >= rootPosition.z + GetHeadshotMinZ( crouching );

	public static bool TryGetCrouching( GameObject target, out bool crouching )
	{
		crouching = false;
		if ( target is null || !target.IsValid() )
			return false;

		var player = target.Components.Get<AimboxPlayerController>( FindMode.EverythingInSelf );
		if ( player is not null )
		{
			crouching = player.IsCrouching;
			return true;
		}

		var bot = target.Components.Get<AimboxBotController>( FindMode.EverythingInSelf );
		if ( bot is not null )
		{
			crouching = bot.IsCrouching;
			return true;
		}

		return false;
	}
}

[Title( "Aimbox Hitbox Debug" )]
[Category( "Aimbox" )]
public sealed class AimboxHitboxDebug : Component
{
	[Property] public string ToggleAction { get; set; } = "DebugHitboxes";
	[Property] public bool DrawOnStart { get; set; }

	bool _enabled;

	protected override void OnStart()
	{
		_enabled = DrawOnStart || AimboxGame.Instance?.EnableHitboxDebug == true;
	}

	protected override void OnUpdate()
	{
		if ( Input.Pressed( ToggleAction ) )
		{
			_enabled = !_enabled;
			Log.Info( $"[Aimbox] Hitbox debug {(_enabled ? "enabled" : "disabled")} (press H to toggle)." );
		}

		if ( !_enabled )
			return;

		DrawPlayerHitboxes();
		DrawDummyHitboxes();
		DebugOverlay.ScreenText( new Vector2( 24f, 72f ), "Hitboxes: ON (H) — cyan = collider, yellow = visual", 0f, TextFlag.LeftTop, Color.Cyan, 1f );
	}

	void DrawPlayerHitboxes()
	{
		var players = AimboxGame.Instance?.Players;
		if ( players is null )
			return;

		foreach ( var player in players )
		{
			if ( player is null || !player.IsValid() )
				continue;

			var color = player.IsAlive ? new Color( 0f, 0.9f, 1f, 1f ) : new Color( 1f, 0.15f, 0.15f, 1f );
			DrawCitizenHitbox( player.GameObject, color, player.IsProxy ? "Player" : "Local Player", player.IsCrouching );
		}
	}

	void DrawDummyHitboxes()
	{
		foreach ( var dummy in Scene.GetAllComponents<AimboxDummyTarget>() )
		{
			if ( dummy is null || !dummy.IsValid() )
				continue;

			if ( dummy.AimCircleMode )
			{
				DrawAimSphereHitbox( dummy );
				continue;
			}

			var color = dummy.IsAlive ? new Color( 1f, 0.78f, 0.1f, 1f ) : new Color( 1f, 0.15f, 0.15f, 1f );
			DrawCitizenHitbox( dummy.GameObject, color, "Dummy", crouching: false );
		}
	}

	void DrawAimSphereHitbox( AimboxDummyTarget dummy )
	{
		var sphere = dummy.Components.Get<SphereCollider>();
		if ( sphere is null )
			return;

		var center = dummy.WorldPosition + dummy.WorldRotation * sphere.Center;
		var colliderRadius = dummy.GetAimSphereColliderWorldRadius();
		var visualRadius = dummy.GetAimSphereVisualWorldRadius();
		var colliderColor = dummy.IsAlive ? Color.Cyan : Color.Red;
		var visualColor = dummy.IsAlive ? new Color( 1f, 0.92f, 0.2f, 1f ) : new Color( 1f, 0.15f, 0.15f, 1f );

		DebugOverlay.Sphere( new Sphere( center, colliderRadius ), colliderColor, 0f, default, false );
		if ( visualRadius > 0.001f )
			DebugOverlay.Sphere( new Sphere( center, visualRadius ), visualColor, 0f, default, false );

		DebugOverlay.Text(
			center + Vector3.Up * (MathF.Max( colliderRadius, visualRadius ) + 10f),
			$"AIM sphere{(dummy.IsAlive ? "" : " (dead)")}\ncollider r={colliderRadius:0.##}\nvisual r={visualRadius:0.##}",
			14f,
			TextFlag.Center,
			colliderColor,
			0f,
			false );
	}

	void DrawCitizenHitbox( GameObject target, Color color, string label, bool crouching )
	{
		if ( target is null || !target.IsValid() )
			return;

		var capsuleStart = AimboxHitboxes.GetCapsuleStart( crouching );
		var capsuleEnd = AimboxHitboxes.GetCapsuleEnd( crouching );
		var headMinZ = AimboxHitboxes.GetHeadshotMinZ( crouching );
		var headTopZ = AimboxHitboxes.GetHeadTopZ( crouching );

		var start = target.WorldPosition + target.WorldRotation * capsuleStart;
		var end = target.WorldPosition + target.WorldRotation * capsuleEnd;
		var headMin = target.WorldPosition.z + headMinZ;
		var headBox = new BBox(
			target.WorldPosition + new Vector3( -AimboxHitboxes.CitizenRadius, -AimboxHitboxes.CitizenRadius, headMinZ ),
			target.WorldPosition + new Vector3( AimboxHitboxes.CitizenRadius, AimboxHitboxes.CitizenRadius, headTopZ ) );

		DebugOverlay.Capsule( new Capsule( start, end, AimboxHitboxes.CitizenRadius ), color, 0f, default, false, 16 );
		DebugOverlay.Box( headBox, Color.Red, 0f, default, false );
		DebugOverlay.Text(
			target.WorldPosition + Vector3.Up * (headTopZ + 10f),
			$"{label} hitbox / head >= {headMin:0}{(crouching ? " (crouch)" : "")}",
			14f,
			TextFlag.Center,
			color,
			0f,
			false );
	}
}
