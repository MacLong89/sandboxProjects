namespace Deep;

/// <summary>Sparse, low-opacity depth cues so vertical progress still reads under sprites.</summary>
public sealed class DepthMarkers : Component
{
	protected override void OnStart()
	{
		SpawnBand( 0f, GameConstants.WaterSunlit.WithAlpha( 0.05f ) );
		SpawnBand( GameConstants.ZoneSunlitEnd, GameConstants.WaterBlue.WithAlpha( 0.06f ) );
		SpawnBand( GameConstants.ZoneBlueEnd, GameConstants.WaterTwilight.WithAlpha( 0.08f ) );
		SpawnBand( GameConstants.ZoneTwilightEnd, new Color( 0.05f, 0.02f, 0.1f, 0.1f ) );

		for ( var d = 50f; d < GameConstants.MaxOceanDepthMeters; d += 50f )
			SpawnTick( d );
	}

	private void SpawnBand( float depthMeters, Color tint )
	{
		var go = new GameObject( GameObject, true, $"Band_{depthMeters:0}" );
		go.WorldPosition = new Vector3( 0f, -2f, GameConstants.WorldZFromDepth( depthMeters ) );
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( GameConstants.HorizontalHalfWidth * 2.2f, 0.15f, 0.25f ) );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = tint;
	}

	private void SpawnTick( float depthMeters )
	{
		var go = new GameObject( GameObject, true, $"Tick_{depthMeters:0}" );
		go.WorldPosition = new Vector3( -GameConstants.HorizontalHalfWidth + 2f, -1.5f, GameConstants.WorldZFromDepth( depthMeters ) );
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( 2.5f, 0.1f, 0.18f ) );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = new Color( 1f, 1f, 1f, 0.15f );
	}
}
