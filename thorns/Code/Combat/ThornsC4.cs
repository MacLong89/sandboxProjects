namespace Sandbox;

/// <summary>Host-authoritative C4 charge: plant via consumable, timed detonation, structure + pawn damage.</summary>
public static class ThornsC4
{
	public const string ItemId = "c4";
	public const float FuseSeconds = 3f;
	public const string ModelPath = "models/placeables/c4.vmdl";

	/// <summary>Target max axis length in world units for planted charge + placement ghost.</summary>
	public const float TargetWorldMaxExtent = 24f;

	/// <summary>First-person held scale (placeable mesh; separate from <see cref="WorldVisualScale"/>).</summary>
	public const float FpViewmodelScale = 4.5f;

	public static readonly Vector3 FpViewmodelRootLocalOffset = new( 18f, 0f, -2f );

	static float _worldVisualScale = -1f;

	/// <summary>Uniform world scale for ghost + planted charge — derived from <see cref="ModelPath"/> bounds when possible.</summary>
	public static float WorldVisualScale
	{
		get
		{
			if ( _worldVisualScale > 0f )
				return _worldVisualScale;

			_worldVisualScale = ComputeWorldVisualScale();
			return _worldVisualScale;
		}
	}

	static float ComputeWorldVisualScale()
	{
		var m = Model.Load( ModelPath );
		if ( m.IsValid() && !m.IsError )
		{
			var size = m.Bounds.Size;
			var maxEdge = MathF.Max( size.x, MathF.Max( size.y, size.z ) );
			if ( maxEdge > 0.01f )
				return TargetWorldMaxExtent / maxEdge;
		}

		Log.Warning( $"[Thorns] {ModelPath} missing or has no bounds — using fallback C4 world scale." );
		return ThornsBuildingVisuals.PlaceableStructureWorldScale * 0.1f;
	}
	public const float BlastRadius = 320f;
	public const float PawnDamageAtCenter = 95f;
	public const float MinDamageFraction = 0.12f;

	public static bool IsEquippedPlacementItem( string itemId ) =>
		string.Equals( itemId, ItemId, StringComparison.OrdinalIgnoreCase );

	/// <summary>Host validates client placement intent — returns authoritative surface position.</summary>
	public static bool HostValidatePlantPosition( GameObject pawnRoot, Vector3 clientPosition, out Vector3 authoritativePosition )
	{
		authoritativePosition = clientPosition;
		if ( !Networking.IsHost || pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		if ( !TryResolvePlantPosition( pawnRoot, out var serverPosition ) )
			return false;

		if ( ( clientPosition - serverPosition ).Length > 160f )
			authoritativePosition = serverPosition;
		else
			authoritativePosition = clientPosition;

		var dist = ( authoritativePosition - pawnRoot.WorldPosition ).Length;
		return dist <= ThornsBuildingDefinitions.MaxPlacementDistance;
	}

	/// <summary>Local ghost + host placement — aim trace with free-placement reach rules.</summary>
	public static bool TryResolvePlantPosition( GameObject pawnRoot, out Vector3 plantPosition )
	{
		plantPosition = default;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var scene = pawnRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var rot ) )
			return false;

		var dir = rot.Forward.Normal;
		var tr = ThornsTraceUtility.RunRay(
			scene,
			new Ray( eye, dir ),
			4000f,
			ThornsTraceProfile.BuildingPlacementView,
			pawnRoot );

		plantPosition = tr.Hit
			? ThornsBuildingSnap.BumpFromTrace( tr.HitPosition, tr.Normal )
			: eye + dir * 420f;

		var dist = ( plantPosition - pawnRoot.WorldPosition ).Length;
		return dist <= ThornsBuildingDefinitions.MaxPlacementDistance;
	}

	public static void HostDetonateAt( Vector3 center, GameObject attackerRoot, ThornsC4Charge chargeForClientSfx = null )
	{
		if ( !Networking.IsHost )
			return;

		HostApplyBlastDamage( center, attackerRoot );
		ThornsBanditHearingHub.HostRegisterExplosion( center );

		if ( Networking.IsActive && chargeForClientSfx is { IsValid: true } )
			chargeForClientSfx.RpcBroadcastDetonateSting( center );
		else
			ThornsWorldSpatialSfx.PlayWorldOneShot(
				ThornsGameplaySfx.BuildMenuOrPlace,
				center,
				ThornsSpatialSfxCategory.PlayerGunshot,
				1.35f );
	}

	static void HostApplyBlastDamage( Vector3 center, GameObject attackerRoot )
	{
		var radius = BlastRadius;
		var radiusSq = radius * radius;
		var ctx = new DamageContext
		{
			AttackerRoot = attackerRoot.IsValid() ? attackerRoot : default,
			Kind = "explosive"
		};

		var structureHits = new List<ThornsPlacedStructure>();
		foreach ( var ps in ThornsPlacedStructure.ActiveByInstanceId.Values )
		{
			if ( ps is not null && ps.IsValid() && ps.GameObject.IsValid() )
				structureHits.Add( ps );
		}

		foreach ( var ps in structureHits )
		{
			if ( ps is null || !ps.IsValid() || !ps.GameObject.IsValid() )
				continue;

			var dSq = ( ps.GameObject.WorldPosition - center ).LengthSquared;
			if ( dSq > radiusSq )
				continue;

			var falloff = HostFalloff( MathF.Sqrt( dSq ), radius );
			var dmg = ThornsBuildingDurability.DamagePerDirectC4 * falloff;
			if ( dmg <= 0.01f )
				continue;

			ThornsBuildingDurability.HostApplyDamage( ps, dmg );
		}

		var playerRoots = ThornsPopulationDirector.HostGetCachedPlayerRoots();
		HostTryBlastDamageHealth( center, radiusSq, radius, attackerRoot, ctx, playerRoots );

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( brain is null || !brain.IsValid() )
				continue;

			HostTryBlastDamageHealth( center, radiusSq, radius, attackerRoot, ctx, brain.Components.Get<ThornsHealth>() );
		}

		foreach ( var brain in ThornsPopulationDirector.HostBanditBrainsReadOnly )
		{
			if ( brain is null || !brain.IsValid() )
				continue;

			HostTryBlastDamageHealth( center, radiusSq, radius, attackerRoot, ctx, brain.Components.Get<ThornsHealth>() );
		}
	}

	static void HostTryBlastDamageHealth(
		Vector3 center,
		float radiusSq,
		float radius,
		GameObject attackerRoot,
		DamageContext ctx,
		ThornsHealth hp )
	{
		if ( hp is null || !hp.IsValid() || !hp.GameObject.IsValid() || !hp.IsAlive )
			return;

		if ( attackerRoot.IsValid() && hp.GameObject == attackerRoot )
			return;

		var dSq = ( hp.GameObject.WorldPosition - center ).LengthSquared;
		if ( dSq > radiusSq )
			return;

		var falloff = HostFalloff( MathF.Sqrt( dSq ), radius );
		var dmg = PawnDamageAtCenter * falloff;
		if ( dmg <= 0.01f )
			return;

		hp.TakeDamage( dmg, ctx );
	}

	static void HostTryBlastDamageHealth(
		Vector3 center,
		float radiusSq,
		float radius,
		GameObject attackerRoot,
		DamageContext ctx,
		IReadOnlyList<GameObject> roots )
	{
		if ( roots is null )
			return;

		foreach ( var root in roots )
		{
			if ( !root.IsValid() )
				continue;

			HostTryBlastDamageHealth( center, radiusSq, radius, attackerRoot, ctx, root.Components.Get<ThornsHealth>() );
		}
	}

	static float HostFalloff( float distance, float radius )
	{
		if ( radius <= 0.01f )
			return 1f;

		var t = Math.Clamp( distance / radius, 0f, 1f );
		var linear = 1f - t;
		return MathF.Max( MinDamageFraction, linear * linear );
	}

}

[Title( "Thorns — C4 Charge" )]
[Category( "Thorns" )]
[Icon( "timer" )]
public sealed class ThornsC4Charge : Component
{
	[Sync( SyncFlags.FromHost )] public double DetonateAtTime { get; set; }

	GameObject _planterRoot;

	public static ThornsC4Charge HostSpawn( Scene scene, Vector3 worldPosition, GameObject planterRoot, Guid planterConnectionId )
	{
		_ = planterConnectionId;

		_ = scene;
		var go = new GameObject( true, "ThornsC4Charge" );
		go.WorldPosition = worldPosition;
		go.LocalScale = Vector3.One * ThornsC4.WorldVisualScale;
		go.Tags.Add( "thorns_c4" );

		var mr = go.Components.Create<ModelRenderer>();
		var c4Model = Model.Load( ThornsC4.ModelPath );
		mr.Model = c4Model;
		mr.Tint = Color.White;
		ThornsModelMaterialUvScale.ApplyScaledModelPresentation( mr, go, c4Model, ThornsC4.ModelPath );

		var charge = go.Components.Create<ThornsC4Charge>();
		charge.DetonateAtTime = Time.Now + ThornsC4.FuseSeconds;
		charge._planterRoot = planterRoot.IsValid() ? planterRoot : null;

		go.NetworkMode = NetworkMode.Object;
		if ( Networking.IsActive && !ThornsNetworkReplication.TryNetworkSpawnHostOwned( go ) )
			Log.Warning( "[Thorns] C4 charge NetworkSpawn failed — joiners may not see this charge." );

		Log.Info( $"[Thorns] C4 planted pos={worldPosition} detonateAt={charge.DetonateAtTime:F2}" );
		return charge;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( Time.Now < DetonateAtTime )
			return;

		var center = GameObject.WorldPosition;
		ThornsC4.HostDetonateAt( center, _planterRoot, this );
		GameObject.Destroy();
	}

	[Rpc.Broadcast]
	public void RpcBroadcastDetonateSting( Vector3 worldEmit ) =>
		ThornsWorldSpatialSfx.PlayWorldOneShot(
			ThornsGameplaySfx.BuildMenuOrPlace,
			worldEmit,
			ThornsSpatialSfxCategory.PlayerGunshot,
			1.35f );
}
