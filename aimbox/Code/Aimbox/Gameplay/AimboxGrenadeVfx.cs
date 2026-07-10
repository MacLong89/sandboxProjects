namespace Sandbox;

static class AimboxGrenadeVfx
{
	const string ExplosionPrefabPath = "prefabs/engine/explosion_med.prefab";
	const string IgnitePrefabPath = "prefabs/engine/ignite.prefab";
	const string SmokePrefabPath = "templates/gameobject/particles - smoke.prefab";
	const string BurstPrefabPath = "templates/gameobject/particles - burst.prefab";

	static PrefabFile _explosion;
	static PrefabFile _ignite;
	static PrefabFile _smoke;
	static PrefabFile _burst;
	static bool _initialized;
	static readonly HashSet<string> _loggedMissing = [];

	public static void PlayDetonation( Scene scene, in AimboxGrenadeConfig config, Vector3 origin )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		if ( Application.IsDedicatedServer || Application.IsHeadless || !Game.IsPlaying )
			return;

		EnsureLoaded();

		switch ( config.Kind )
		{
			case AimboxGrenadeKind.Explosive:
				SpawnEffect( scene, _explosion, origin, 2.5f );
				break;
			case AimboxGrenadeKind.Flash:
				break;
			case AimboxGrenadeKind.Smoke:
				SpawnEffect( scene, _smoke, origin, Math.Max( 4f, config.EffectDurationSeconds ), scale: 8.8f );
				break;
			case AimboxGrenadeKind.Decoy:
				SpawnEffect( scene, _burst, origin, 1.6f, scale: 1.35f );
				break;
			case AimboxGrenadeKind.Incendiary:
				SpawnEffect(
					scene,
					_ignite,
					SnapToFloor( scene, origin ),
					Math.Max( 2f, config.EffectDurationSeconds ),
					scale: Math.Max( 1.5f, config.BlastRadius / 42f ) );
				break;
		}
	}

	static void EnsureLoaded()
	{
		if ( _initialized )
			return;

		_initialized = true;
		_explosion = ResourceLibrary.Get<PrefabFile>( ExplosionPrefabPath );
		_ignite = ResourceLibrary.Get<PrefabFile>( IgnitePrefabPath );
		_smoke = ResourceLibrary.Get<PrefabFile>( SmokePrefabPath );
		_burst = ResourceLibrary.Get<PrefabFile>( BurstPrefabPath );

		if ( _explosion is null )
			LogMissingOnce( ExplosionPrefabPath );
		if ( _ignite is null )
			LogMissingOnce( IgnitePrefabPath );
		if ( _smoke is null )
			LogMissingOnce( SmokePrefabPath );
		if ( _burst is null )
			LogMissingOnce( BurstPrefabPath );
	}

	static void SpawnEffect( Scene scene, PrefabFile prefab, Vector3 origin, float lifetime, float scale = 1f )
	{
		if ( prefab is null )
			return;

		var template = SceneUtility.GetPrefabScene( prefab );
		if ( template is null || !template.IsValid() )
			return;

		var instance = template.Clone();
		if ( !instance.IsValid() )
			return;

		instance.WorldPosition = origin;
		instance.WorldScale = Vector3.One * scale;
		instance.Parent = scene;
		StripBuiltinDamage( instance );

		var temp = instance.Components.Create<AimboxTemporaryVfx>();
		temp.Init( lifetime );
	}

	static Vector3 SnapToFloor( Scene scene, Vector3 origin )
	{
		var tr = scene.Trace.Ray( origin + Vector3.Up * 48f, origin + Vector3.Down * 320f ).Run();
		return tr.Hit ? tr.EndPosition + tr.Normal * 2f : origin;
	}

	static void StripBuiltinDamage( GameObject root )
	{
		foreach ( var component in root.Components.GetAll<Component>( FindMode.EverythingInSelfAndChildren ) )
		{
			if ( component is null )
				continue;

			var typeName = component.GetType().Name;
			if ( typeName is "RadiusDamage" or "FireDamage" )
				component.Destroy();
		}
	}

	static void LogMissingOnce( string path )
	{
		if ( !_loggedMissing.Add( path ) )
			return;

		Log.Warning( $"[Aimbox VFX] Missing grenade prefab '{path}'." );
	}
}

[Title( "Aimbox Temporary VFX" )]
[Category( "Aimbox" )]
sealed class AimboxTemporaryVfx : Component
{
	TimeUntil _lifetime;

	public void Init( float seconds ) => _lifetime = Math.Max( 0.1f, seconds );

	protected override void OnUpdate()
	{
		if ( (float)_lifetime > 0f )
			return;

		GameObject.Destroy();
	}
}
