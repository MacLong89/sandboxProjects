namespace Terraingen.Combat;

using Sandbox.Network;
using Terraingen.Multiplayer;
using Terraingen.Rendering;

/// <summary>Placed C4 — short fuse, radial detonation, then self-destructs.</summary>
[Title( "Thorns Explosive Charge" )]
[Category( "Thorns/Combat" )]
public sealed class ThornsExplosiveCharge : Component
{
	public const float FuseSeconds = 3f;
	public const float ExplosionRadius = ThornsCombatExplosion.DefaultRadius;
	public const float VisualScale = 28f;

	[Sync( SyncFlags.FromHost )] public float FuseRemaining { get; private set; } = FuseSeconds;
	[Sync( SyncFlags.FromHost )] public string OwnerAccountKey { get; private set; } = "";

	GameObject _attackerRoot;
	TimeUntil _detonateIn;
	bool _detonated;

	public static ThornsExplosiveCharge SpawnHost(
		Scene scene,
		Connection owner,
		Vector3 position,
		Rotation rotation,
		GameObject placerRoot )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || scene is null || !scene.IsValid() )
			return null;

		var go = scene.CreateObject( true );
		go.Name = "C4 Charge";
		go.WorldPosition = position;
		go.WorldRotation = rotation;
		go.Tags.Add( "thorns_explosive" );
		go.NetworkMode = NetworkMode.Object;

		ApplyVisual( go );

		var charge = go.Components.Create<ThornsExplosiveCharge>();
		charge.OwnerAccountKey = owner is null
			? ""
			: Terraingen.Multiplayer.ThornsPersistenceIdentity.GetStableAccountKey( owner );
		charge._attackerRoot = placerRoot.IsValid() ? placerRoot : null;
		charge.FuseRemaining = FuseSeconds;
		charge._detonateIn = FuseSeconds;

		if ( Networking.IsActive )
		{
			var opts = new NetworkSpawnOptions
			{
				Owner = Connection.Host,
				OrphanedMode = NetworkOrphaned.Host
			};
			go.NetworkSpawn( opts );
		}

		return charge;
	}

	protected override void OnFixedUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || _detonated )
			return;

		FuseRemaining = MathF.Max( 0f, _detonateIn );
		if ( _detonateIn )
			return;

		HostDetonate();
	}

	void HostDetonate()
	{
		if ( _detonated || !GameObject.IsValid() )
			return;

		_detonated = true;
		FuseRemaining = 0f;

		var center = GameObject.WorldPosition + Vector3.Up * (VisualScale * 0.35f);
		ThornsCombatExplosion.HostDetonate( center, _attackerRoot, ExplosionRadius );
		GameObject.Destroy();
	}

	public static void ApplyVisual( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		foreach ( var child in go.Children.ToArray() )
			child.Destroy();

		var model = Model.Load( "models/dev/box.vmdl" );
		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.Tint = new Color( 0.92f, 0.22f, 0.08f );
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );
		go.LocalScale = Vector3.One * (VisualScale / 50f);

		var collider = go.Components.Create<BoxCollider>();
		collider.Scale = Vector3.One * VisualScale;
		collider.Static = true;
	}
}
