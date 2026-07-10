using System;

namespace Sandbox;

/// <summary>
/// Opt-in trace overlay (last N rays). Off by default — F8 / developer panel. Does not run in release unless toggled.
/// </summary>
public static class ThornsTraceDebug
{
	public const int RingCapacity = 40;

	public static bool ShowTraceOverlay { get; set; }

	static readonly ThornsTraceDebugSample[] _ring = new ThornsTraceDebugSample[RingCapacity];

	static int _head;

	public static void Record( ThornsTraceProfile profile, in Ray ray, float distance, in SceneTraceResult tr )
	{
		if ( !ShowTraceOverlay )
			return;

		var i = _head++ % RingCapacity;
		_ring[i] = new ThornsTraceDebugSample( profile, ray, distance, tr, Time.Now );
	}

	public static void TickDraw( Scene scene )
	{
		if ( !ShowTraceOverlay || scene is null || !scene.IsValid() || !Game.IsPlaying )
			return;

		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is null )
			return;

		const float dur = 0.18f;
		for ( var k = 0; k < RingCapacity; k++ )
		{
			var s = _ring[k];
			if ( s.Time <= 0 )
				continue;

			var age = Time.Now - s.Time;
			if ( age > 2.5 )
				continue;

			var end = s.Result.Hit && s.Result.GameObject.IsValid()
				? s.Result.HitPosition
				: s.Ray.Position + s.Ray.Forward * Math.Min( s.Distance, 900f );

			var col = s.Profile switch
			{
				ThornsTraceProfile.WeaponHitscan or ThornsTraceProfile.WeaponFeedbackWorld => new Color( 1f, 0.2f, 0.2f, 0.55f ),
				ThornsTraceProfile.InteractionUse => new Color( 0.2f, 0.95f, 1f, 0.55f ),
				ThornsTraceProfile.BuildingPlacementView or ThornsTraceProfile.BuildingStructurePickPiercing
					or ThornsTraceProfile.BuildingTerrainSupportDown => new Color( 1f, 0.92f, 0.2f, 0.55f ),
				ThornsTraceProfile.MovementProbe => new Color( 0.35f, 0.95f, 0.45f, 0.55f ),
				ThornsTraceProfile.AiLineOfSight => new Color( 1f, 0.35f, 1f, 0.55f ),
				ThornsTraceProfile.TamingWorldPick => new Color( 0.95f, 0.55f, 0.2f, 0.55f ),
				_ => new Color( 1f, 1f, 1f, 0.45f )
			};

			dbg.Line( s.Ray.Position, end, col, dur, default, false );
			if ( s.Result.Hit && s.Result.GameObject.IsValid() )
			{
				var n = s.Result.Normal.Normal * 8f;
				dbg.Line( end, end + n, new Color( 1f, 0.55f, 0.1f, 0.65f ), dur, default, false );
			}
		}
	}

	public readonly struct ThornsTraceDebugSample
	{
		public ThornsTraceDebugSample(
			ThornsTraceProfile profile,
			in Ray ray,
			float distance,
			in SceneTraceResult result,
			double time )
		{
			Profile = profile;
			Ray = ray;
			Distance = distance;
			Result = result;
			Time = time;
		}

		public ThornsTraceProfile Profile { get; }

		public Ray Ray { get; }

		public float Distance { get; }

		public SceneTraceResult Result { get; }

		public double Time { get; }
	}
}
