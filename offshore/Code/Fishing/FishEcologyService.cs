namespace Offshore;

public sealed class EcologyContext
{
	public float Distance;
	public float WaterDepth;
	public float LureDepth;
	public string BaitId;
	public DayPhase Time;
	public WeatherType Weather;
	public float Temperature;
	public float Clarity;
	public string Seabed;
	public string Structure;
	public float HotspotStrength;
	public string HotspotSpeciesBias;
	public float LineVisibility;
	public float Noise;
	public float BoatSpeed;
	public float Moonlight;
	public float Wind;
	public float Rain;
	public HookDefinition Hook;
	public float CastAccuracy;
}

public static class FishEcologyService
{
	public static FishDefinition RollSpecies( EcologyContext ctx, Random rng )
	{
		var weights = new List<(FishDefinition fish, float w)>();
		foreach ( var fish in Catalog.Fish )
		{
			var w = Weight( fish, ctx );
			if ( w > 0.01f )
				weights.Add( (fish, w) );
		}

		if ( weights.Count == 0 )
			return Catalog.Fish[0];

		var total = weights.Sum( x => x.w );
		var roll = (float)rng.NextDouble() * total;
		foreach ( var (fish, w) in weights )
		{
			roll -= w;
			if ( roll <= 0 )
				return fish;
		}
		return weights[^1].fish;
	}

	public static float Weight( FishDefinition f, EcologyContext ctx )
	{
		if ( ctx.Distance < f.MinDistance * 0.7f || ctx.Distance > f.MaxDistance * 1.15f )
			return 0f;
		if ( ctx.WaterDepth < f.MinDepth * 0.6f )
			return 0f;

		var rarityMul = f.Rarity switch
		{
			Rarity.Common => 1.4f,
			Rarity.Uncommon => 0.9f,
			Rarity.Rare => 0.45f,
			Rarity.Epic => 0.18f,
			Rarity.Legendary => 0.06f,
			_ => 1f
		};

		var w = 10f * rarityMul;
		w *= DistMod( ctx.Distance, f.MinDistance, f.PreferredDistance, f.MaxDistance );
		w *= DistMod( ctx.LureDepth, f.MinDepth, f.PreferredLureDepth, f.MaxDepth );
		w *= BaitMod( f, ctx.BaitId );
		w *= TimeMod( f.PreferredTime, ctx.Time );
		w *= WeatherMod( f.PreferredWeather, ctx.Weather );
		w *= SoftMatch( ctx.Temperature, f.PreferredTemp, 8f );
		w *= SoftMatch( ctx.Clarity, f.PreferredClarity, 0.45f );
		w *= ctx.Seabed == f.PreferredSeabed ? 1.25f : 0.85f;
		w *= ctx.Structure == f.PreferredStructure ? 1.2f : 0.9f;
		w *= 1f + f.HotspotAffinity * ctx.HotspotStrength * (ctx.HotspotSpeciesBias == f.Id ? 1.5f : 0.6f);
		w *= 1f - f.LineVisibilitySensitivity * ctx.LineVisibility * 0.45f;
		w *= 1f - f.NoiseSensitivity * Math.Clamp( ctx.Noise + ctx.BoatSpeed / 80f, 0f, 1.2f ) * 0.4f;
		w *= 1f + f.MoonlightAffinity * ctx.Moonlight * 0.5f;
		w *= 1f + f.WindAffinity * ctx.Wind * 0.25f;
		w *= 1f + f.RainAffinity * ctx.Rain * 0.3f;
		w *= 1f + f.SurfaceAffinity * Math.Clamp( 1f - ctx.LureDepth / 30f, 0f, 1f ) * 0.3f;

		if ( ctx.Hook is not null )
		{
			if ( f.MaxWeight > ctx.Hook.MaxFishSize )
				w *= 0.55f + ctx.Hook.LargeFishBonus;
			if ( f.MaxWeight < 2f )
				w *= 1f - ctx.Hook.SmallFishPenalty;
		}

		w *= 0.75f + 0.5f * ctx.CastAccuracy;
		return Math.Max( 0f, w );
	}

	public static CaughtFish CreateCatch( FishDefinition def, Random rng, bool personalBestHint = false )
	{
		var t = (float)rng.NextDouble();
		var quality = 0.75f + t * 0.5f;
		var length = Lerp( def.MinLength, def.MaxLength, t );
		var weight = Lerp( def.MinWeight, def.MaxWeight, MathF.Pow( t, 0.85f ) );
		var rarityMul = def.Rarity switch
		{
			Rarity.Uncommon => 1.35f,
			Rarity.Rare => 1.8f,
			Rarity.Epic => 2.6f,
			Rarity.Legendary => 4f,
			_ => 1f
		};
		var value = Math.Max( 1, (int)(def.BaseValue * 1.35f * (0.65f + weight / Math.Max( 0.1f, def.MaxWeight )) * quality * rarityMul) );
		return new CaughtFish
		{
			SpeciesId = def.Id,
			SpeciesName = def.Name,
			Rarity = def.Rarity,
			Length = length,
			Weight = weight,
			Quality = quality,
			Value = value,
			PersonalBest = personalBestHint
		};
	}

	static float DistMod( float v, float min, float pref, float max )
	{
		if ( v < min ) return Math.Clamp( 1f - (min - v) / Math.Max( 1f, min ), 0.05f, 1f );
		if ( v > max ) return Math.Clamp( 1f - (v - max) / Math.Max( 1f, max * 0.35f ), 0.02f, 1f );
		var span = Math.Max( 1f, Math.Max( pref - min, max - pref ) );
		return 1.15f - Math.Abs( v - pref ) / span * 0.55f;
	}

	static float BaitMod( FishDefinition f, string bait )
	{
		if ( string.IsNullOrEmpty( bait ) ) return 0.35f;
		if ( f.PreferredBait == bait ) return 1.7f;
		if ( f.SecondaryBait == bait ) return 1.25f;
		var def = Catalog.BaitById( bait );
		if ( def is null ) return 0.6f;
		if ( def.PrimaryTargets.Contains( f.Id ) ) return 1.55f;
		if ( def.SecondaryTargets.Contains( f.Id ) ) return 1.2f;
		return 0.65f + def.RareAffinity * (f.Rarity >= Rarity.Rare ? 0.4f : 0f);
	}

	static float TimeMod( DayPhase prefer, DayPhase now )
	{
		if ( prefer == now ) return 1.35f;
		var adj = Math.Abs( (int)prefer - (int)now );
		return adj <= 1 ? 1.05f : 0.75f;
	}

	static float WeatherMod( WeatherType prefer, WeatherType now )
	{
		if ( prefer == now ) return 1.3f;
		var rainy = prefer is WeatherType.LightRain or WeatherType.HeavyRain or WeatherType.Thunderstorm;
		var nowRain = now is WeatherType.LightRain or WeatherType.HeavyRain or WeatherType.Thunderstorm;
		if ( rainy && nowRain ) return 1.1f;
		return 0.8f;
	}

	static float SoftMatch( float value, float prefer, float range )
	{
		var d = Math.Abs( value - prefer ) / Math.Max( 0.01f, range );
		return Math.Clamp( 1.2f - d * 0.55f, 0.35f, 1.2f );
	}

	static float Lerp( float a, float b, float t ) => a + (b - a) * Math.Clamp( t, 0f, 1f );
}
