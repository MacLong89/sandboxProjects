namespace Sandbox;

/// <summary>District selection, neighbor influence, and building-type sampling for clusters.</summary>
public sealed class ThornsProcBuildingDistrictPlanner
{
	readonly Random _rnd;
	readonly int _typeCount = ThornsProcBuildingIdentityRegistry.TypeCount;
	readonly List<ThornsProcBuildingType> _clusterHistory = new( 32 );

	public ThornsProcBuildingDistrict ClusterDistrict { get; private set; } = ThornsProcBuildingDistrict.Mixed;

	public ThornsProcBuildingDistrictPlanner( int seed )
	{
		_rnd = new Random( seed );
	}

	/// <summary>Pick a district identity for a new town cluster center.</summary>
	public void BeginCluster( ThornsProcBuildingDistrict? forceDistrict = null )
	{
		_clusterHistory.Clear();
		if ( forceDistrict.HasValue )
		{
			ClusterDistrict = forceDistrict.Value;
			return;
		}

		var t = _rnd.NextDouble();
		ClusterDistrict = t switch
		{
			< 0.28 => ThornsProcBuildingDistrict.Residential,
			< 0.46 => ThornsProcBuildingDistrict.Industrial,
			< 0.58 => ThornsProcBuildingDistrict.Commercial,
			< 0.68 => ThornsProcBuildingDistrict.Rural,
			< 0.78 => ThornsProcBuildingDistrict.Military,
			_ => ThornsProcBuildingDistrict.Mixed
		};
	}

	/// <summary>Blend cluster district toward another when near map edges or secondary clusters (0–1 blend).</summary>
	public void BlendClusterDistrict( ThornsProcBuildingDistrict other, float blendTowardOther )
	{
		if ( blendTowardOther <= 0.01f )
			return;

		if ( blendTowardOther >= 0.99f )
		{
			ClusterDistrict = other;
			return;
		}

		// Soft mix: pick stochastically weighted by blend.
		if ( _rnd.NextDouble() < blendTowardOther )
			ClusterDistrict = other;
	}

	/// <summary>Sample next building type for this cluster slot.</summary>
	public ThornsProcBuildingType PickBuildingType( bool isolatedSite )
	{
		var src = ThornsProcBuildingIdentityRegistry.DistrictBaseWeights[ClusterDistrict];
		var weights = new float[_typeCount];
		Array.Copy( src, weights, Math.Min( src.Length, _typeCount ) );

		if ( isolatedSite )
		{
			// Rural isolation: boost cabin/barn/ruin.
			Add( weights, ThornsProcBuildingType.Cabin, 12f );
			Add( weights, ThornsProcBuildingType.Barn, 10f );
			Add( weights, ThornsProcBuildingType.Ruin, 6f );
			Mul( weights, ThornsProcBuildingType.Apartment, 0.35f );
			Mul( weights, ThornsProcBuildingType.MilitaryComplex, 0.4f );
		}

		ApplyNeighborInfluence( weights );
		return SampleWeights( weights );
	}

	/// <summary>Organic placement: district weights × inverse footprint size.</summary>
	public ThornsProcBuildingType PickBuildingTypeForOrganic( bool isolatedSite = false )
	{
		var src = ThornsProcBuildingIdentityRegistry.DistrictBaseWeights[ClusterDistrict];
		var weights = new float[_typeCount];
		Array.Copy( src, weights, Math.Min( src.Length, _typeCount ) );

		for ( var t = 0; t < _typeCount; t++ )
		{
			var type = (ThornsProcBuildingType)t;
			weights[t] *= ThornsProcBuildingIdentityRegistry.OrganicSpawnSizeWeight( type );
			if ( ThornsProcBuildingIdentityRegistry.IsVerticalLandmark( type ) )
				weights[t] *= 2.2f;
		}

		if ( isolatedSite )
		{
			Add( weights, ThornsProcBuildingType.Cabin, 12f );
			Add( weights, ThornsProcBuildingType.Barn, 10f );
			Add( weights, ThornsProcBuildingType.Ruin, 6f );
			Mul( weights, ThornsProcBuildingType.Apartment, 0.35f );
			Mul( weights, ThornsProcBuildingType.MilitaryComplex, 0.4f );
		}

		ApplyNeighborInfluence( weights );
		return SampleWeights( weights );
	}

	void ApplyNeighborInfluence( float[] weights )
	{
		var recent = _clusterHistory.Count;
		if ( recent == 0 )
			return;

		// Last 3 placements in cluster strongly influence next.
		var start = Math.Max( 0, recent - 3 );
		for ( var i = start; i < recent; i++ )
		{
			var neighbor = _clusterHistory[i];
			if ( !ThornsProcBuildingIdentityRegistry.AdjacencyBoost.TryGetValue( neighbor, out var boost ) )
				continue;

			for ( var t = 0; t < _typeCount; t++ )
				weights[t] *= boost[t];
		}

		// Military suppresses residential nearby.
		if ( _clusterHistory.TakeLast( 4 ).Any( t => t == ThornsProcBuildingType.MilitaryComplex ) )
		{
			Mul( weights, ThornsProcBuildingType.House, 0.55f );
			Mul( weights, ThornsProcBuildingType.Apartment, 0.5f );
			Mul( weights, ThornsProcBuildingType.Store, 0.65f );
		}
	}

	public void RegisterPlaced( ThornsProcBuildingType type )
	{
		_clusterHistory.Add( type );
	}

	/// <summary>Sample from custom weight table (applies neighbor influence, does not reset cluster district).</summary>
	public ThornsProcBuildingType PickFromWeights( float[] weights )
	{
		var copy = new float[_typeCount];
		if ( weights is not null )
			Array.Copy( weights, copy, Math.Min( weights.Length, _typeCount ) );

		ApplyNeighborInfluence( copy );
		return SampleWeights( copy );
	}

	public float SpacingMultiplierFor( ThornsProcBuildingType type )
	{
		var def = ThornsProcBuildingIdentityRegistry.Get( type );
		var districtMul = ClusterDistrict switch
		{
			ThornsProcBuildingDistrict.Rural => 1.18f,
			ThornsProcBuildingDistrict.Industrial => 1.12f,
			ThornsProcBuildingDistrict.Military => 1.28f,
			ThornsProcBuildingDistrict.Residential => 0.92f,
			ThornsProcBuildingDistrict.Commercial => 0.98f,
			_ => 1f
		};

		return def.ClusterSpacingMul * districtMul;
	}

	static void Add( float[] w, ThornsProcBuildingType type, float add ) => w[(int)type] += add;
	static void Mul( float[] w, ThornsProcBuildingType type, float mul ) => w[(int)type] *= mul;

	ThornsProcBuildingType SampleWeights( float[] weights )
	{
		var total = 0f;
		for ( var i = 0; i < _typeCount; i++ )
			total += Math.Max( 0f, weights[i] );

		if ( total <= 0.01f )
			return ThornsProcBuildingType.House;

		var roll = _rnd.NextDouble() * total;
		for ( var i = 0; i < _typeCount; i++ )
		{
			roll -= Math.Max( 0f, weights[i] );
			if ( roll <= 0 )
				return (ThornsProcBuildingType)i;
		}

		return ThornsProcBuildingType.House;
	}
}
