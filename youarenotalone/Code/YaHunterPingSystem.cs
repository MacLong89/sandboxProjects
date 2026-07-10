namespace Sandbox;

/// <summary>Not Alone: place a short-lived world ping (default X). Host validates, all clients render marker.</summary>
[Title( "YouAreNotAlone — Hunter ping" )]
[Category( "YouAreNotAlone" )]
[Icon( "place" )]
[Order( 410 )]
public sealed class YaHunterPingSystem : Component
{
	const float PingCooldownSeconds = 2.5f;
	const float PingMaxDistance = 2800f;

	double _nextPingAllowedAt;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( !YaRoundGate.MayUseWeapons() )
			return;

		var role = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !role.IsValid() || role.Role != YaPlayerRole.NotAlone )
			return;

		var hp = Components.Get<YaPlayerHealth>();
		if ( hp.IsValid() && hp.IsDeadState )
			return;

		if ( !PingPressed() )
			return;

		if ( Time.Now < _nextPingAllowedAt )
			return;

		_nextPingAllowedAt = Time.Now + PingCooldownSeconds;

		if ( !YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out var eyePos, out var eyeRot ) )
			return;

		var end = eyePos + eyeRot.Forward * PingMaxDistance;
		var tr = Scene.Trace.Ray( eyePos, end )
			.UseHitPosition( true )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		var hit = tr.Hit ? tr.HitPosition : end;
		if ( Networking.IsHost )
			HostPlacePing( hit, Connection.Local?.DisplayName ?? "Hunter" );
		else
			RequestPingRpc( hit );
	}

	static bool PingPressed()
	{
		return Input.Pressed( "use" ) || Input.Pressed( "Use" )
		       || Input.Keyboard.Pressed( "x" ) || Input.Keyboard.Pressed( "X" );
	}

	[Rpc.Host]
	void RequestPingRpc( Vector3 worldPos )
	{
		if ( !YaPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		var role = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !role.IsValid() || role.Role != YaPlayerRole.NotAlone )
			return;

		var name = Rpc.Caller?.DisplayName ?? "Hunter";
		HostPlacePing( worldPos, name );
	}

	void HostPlacePing( Vector3 worldPos, string ownerLabel )
	{
		if ( !Networking.IsHost )
			return;

		RpcBroadcastPingVisual( worldPos, ownerLabel );
	}

	[Rpc.Broadcast]
	void RpcBroadcastPingVisual( Vector3 worldPos, string ownerLabel )
	{
		YaPingWorldMarker.SpawnLocal( worldPos, ownerLabel );
	}
}

/// <summary>Client-only ping marker mesh + light.</summary>
public static class YaPingWorldMarker
{
	public const float LifeSeconds = 5f;

	public static void SpawnLocal( Vector3 worldPos, string ownerLabel )
	{
		var go = new GameObject( true, "YaPingMarker" );
		go.WorldPosition = worldPos + Vector3.Up * 8f;

		var light = go.Components.Create<PointLight>();
		light.LightColor = new Color( 0.25f, 0.92f, 1f, 1f );
		light.Radius = 96f;

		var ring = new GameObject( true, "YaPingRing" );
		ring.SetParent( go );
		ring.LocalPosition = Vector3.Zero;
		var mr = ring.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 0.2f, 0.95f, 1f, 0.55f );
		ring.LocalScale = new Vector3( 0.35f );

		var pillar = new GameObject( true, "YaPingPillar" );
		pillar.SetParent( go );
		pillar.LocalPosition = new Vector3( 0f, 0f, 48f );
		var pillarLight = pillar.Components.Create<PointLight>();
		pillarLight.LightColor = new Color( 0.15f, 0.85f, 1f, 1f );
		pillarLight.Radius = 140f;

		var life = go.Components.Create<YaDestroyAfterSeconds>();
		life.Seconds = LifeSeconds;
	}
}
