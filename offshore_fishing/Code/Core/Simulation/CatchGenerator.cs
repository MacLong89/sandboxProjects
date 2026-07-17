namespace OffshoreFishing.Core;

public static class CatchGenerator
{
	public static CaughtFish Generate( GameContent content, GameState state, SeededRng rng, string forcedFishId = null )
	{
		var fish = ResolveFish( content, state, rng, forcedFishId );
		var sizeT = MathF.Pow( rng.NextFloat(), 0.85f );
		var sizeCm = Lerp( fish.MinCm, fish.MaxCm, sizeT );
		var weightKg = Lerp( fish.MinKg, fish.MaxKg, sizeT * rng.NextFloat( 0.9f, 1.1f ) );
		weightKg = Math.Clamp( weightKg, fish.MinKg, fish.MaxKg );
		var quality = Math.Clamp( rng.NextFloat( 0.35f, 1f ), 0f, 1f );
		var rarity = Economy.RollRarity( fish, rng, content, state );
		var worth = Economy.ComputeWorth( fish, sizeCm, quality, rarity, content, state );

		return new CaughtFish
		{
			InstanceId = Guid.NewGuid().ToString( "N" ),
			FishId = fish.Id,
			SizeCm = MathF.Round( sizeCm, 1 ),
			WeightKg = MathF.Round( weightKg, 2 ),
			Rarity = rarity,
			Quality = MathF.Round( quality, 2 ),
			Worth = worth,
			ZoneId = state.CurrentZoneId,
			CaughtAtUtc = DateTimeOffset.UtcNow
		};
	}

	private static FishDef ResolveFish( GameContent content, GameState state, SeededRng rng, string forcedFishId )
	{
		if ( !string.IsNullOrEmpty( forcedFishId ) && content.TryGetFish( forcedFishId, out var forced ) )
			return forced;

		if ( !state.TutorialFirstCatchDone )
		{
			var firstId = content.Tutorial.GuaranteedFirstFishId;
			if ( content.TryGetFish( firstId, out var first ) )
				return first;
		}

		if ( state.TotalCatches + 1 >= content.Tutorial.GuaranteedUncommonByCatch
			&& !state.FishLog.ContainsKey( content.Tutorial.FirstUncommonFishId )
			&& content.TryGetFish( content.Tutorial.FirstUncommonFishId, out var uncommon ) )
		{
			return uncommon;
		}

		var zone = content.GetZone( state.CurrentZoneId );
		var rodTier = content.TryGetItem( state.EquippedRodId, out var rod ) ? rod.Tier : 0;
		var hookTier = content.TryGetItem( state.EquippedHookId, out var hook ) ? hook.Tier : 0;
		var depth = Math.Max( state.BoatDepthM, state.Fishing.HookDepthM );

		var candidates = zone.FishIds
			.Select( id => content.GetFish( id ) )
			.Where( f => f.RequiredRodTier <= rodTier && f.RequiredHookTier <= hookTier )
			.Where( f => depth >= f.MinDepth * 0.5f )
			.ToList();

		if ( candidates.Count == 0 )
		{
			candidates = zone.FishIds.Select( id => content.GetFish( id ) ).ToList();
		}

		return rng.PickWeighted( candidates, f =>
		{
			var w = f.SpawnWeight;
			if ( state.EquippedBaitId != null && f.PreferredBait.Contains( state.EquippedBaitId ) )
				w *= 1.75f;

			// Pity: boost undiscovered fish after many catches.
			if ( !state.FishLog.ContainsKey( f.Id ) && state.TotalCatches > 12 )
				w *= 1.35f;

			var depthCenter = (f.MinDepth + f.MaxDepth) * 0.5f;
			var depthDelta = MathF.Abs( depth - depthCenter );
			var depthFactor = Math.Clamp( 1.4f - depthDelta / Math.Max( 20f, f.MaxDepth ), 0.35f, 1.4f );
			return w * depthFactor;
		} );
	}

	private static float Lerp( float a, float b, float t ) => a + (b - a) * Math.Clamp( t, 0f, 1f );
}
