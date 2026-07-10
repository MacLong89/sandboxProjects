namespace Sandbox;

/// <summary>
/// Host-scheduled idle calls for vocal species — <see cref="Rpc.Broadcast"/> plays world 3D audio on every peer (same pattern as <see cref="ThornsBanditCombat"/> gunshot).
/// </summary>
[Title( "Thorns — Wildlife vocal" )]
[Category( "Thorns/Wildlife" )]
[Icon( "mic" )]
[Order( 13 )]
public sealed class ThornsWildlifeVocalization : Component
{
	const float TickSeconds = 0.4f;
	const float CooldownMin = 16f;
	const float CooldownMax = 38f;

	float _accum;

	/// <summary>Host realtime for next allowed vocal attempt.</summary>
	double _nextVocalEligibleTime;

	/// <summary>Set in <see cref="OnStart"/> — avoids ticking non-vocal species every frame budget.</summary>
	bool _speciesHasVocalClip;

	protected override void OnStart()
	{
		_nextVocalEligibleTime = Time.Now + Random.Shared.NextDouble() * 9.0 + 2.0;
		var id0 = Components.Get<ThornsWildlifeIdentity>();
		_speciesHasVocalClip = id0.IsValid() && TryGetVocalSoundPath( id0.Species, out _ );
	}

	protected override void OnUpdate()
	{
		var authoritative = !Networking.IsActive || Networking.IsHost;
		if ( !authoritative || !Game.IsPlaying || !_speciesHasVocalClip )
			return;

		_accum += Time.Delta;
		if ( _accum < TickSeconds )
			return;

		_accum = 0;

		if ( Time.Now < _nextVocalEligibleTime )
			return;

		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( !id.IsValid() || !TryGetVocalSoundPath( id.Species, out var path ) )
			return;

		var hp = Components.Get<ThornsHealth>();
		if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
			return;

		var director = ThornsWildlifeDirector.Instance;
		if ( director is null || !director.IsValid() )
			return;

		var flat = GameObject.WorldPosition.WithZ( 0 );
		var nearestSq = director.HostNearestPlayerDistSq( flat );
		var lod = ThornsWildlifeLOD.ComputeTier( nearestSq );
		if ( lod == ThornsWildlifeLodTier.Dormant )
		{
			_nextVocalEligibleTime = Time.Now + Random.Shared.NextDouble() * 4.0 + 2.5;
			return;
		}

		_nextVocalEligibleTime = Time.Now + HostSampleCooldownSeconds( lod );

		var localOffset = Vector3.Up * MouthHeightUnits( id.Species );
		if ( Networking.IsActive )
			RpcBroadcastWildlifeVocalWorld( path );
		else
			ThornsWorldSpatialSfx.PlayWorldOneShotFollowing(
				GameObject,
				localOffset,
				path.Trim(),
				ThornsSpatialSfxCategory.WildlifeVocal );
	}

	static bool TryGetVocalSoundPath( ThornsWildlifeSpeciesKind species, out string path )
	{
		switch ( species )
		{
			case ThornsWildlifeSpeciesKind.Panther:
				path = "sounds/wildlife_vocal_panther.sound";
				return true;
			case ThornsWildlifeSpeciesKind.Deer:
				path = "sounds/wildlife_vocal_deer.sound";
				return true;
			case ThornsWildlifeSpeciesKind.Moose:
				path = "sounds/wildlife_vocal_moose.sound";
				return true;
			case ThornsWildlifeSpeciesKind.Wolf:
				path = "sounds/wildlife_vocal_wolf.sound";
				return true;
			default:
				path = null;
				return false;
		}
	}

	static float MouthHeightUnits( ThornsWildlifeSpeciesKind species ) =>
		species switch
		{
			ThornsWildlifeSpeciesKind.Moose => 78f,
			ThornsWildlifeSpeciesKind.Deer => 54f,
			ThornsWildlifeSpeciesKind.Wolf => 44f,
			ThornsWildlifeSpeciesKind.Panther => 40f,
			_ => 48f
		};

	static double HostSampleCooldownSeconds( ThornsWildlifeLodTier lod )
	{
		var span = Random.Shared.NextSingle() * ( CooldownMax - CooldownMin ) + CooldownMin;
		var lodMul = lod switch
		{
			ThornsWildlifeLodTier.Near => 1f,
			ThornsWildlifeLodTier.Mid => 1.2f,
			ThornsWildlifeLodTier.Far => 1.55f,
			_ => 1.8f
		};
		var jitter = Random.Shared.NextSingle() * 0.28f + 0.86f;
		return span * lodMul * jitter;
	}

	[Rpc.Broadcast]
	void RpcBroadcastWildlifeVocalWorld( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || !GameObject.IsValid() )
			return;

		var id = Components.Get<ThornsWildlifeIdentity>();
		var localOffset = Vector3.Up * ( id.IsValid() ? MouthHeightUnits( id.Species ) : 48f );
		ThornsWorldSpatialSfx.PlayWorldOneShotFollowing(
			GameObject,
			localOffset,
			resourcePath.Trim(),
			ThornsSpatialSfxCategory.WildlifeVocal );
	}
}
