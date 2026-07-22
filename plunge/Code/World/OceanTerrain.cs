using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plunge;

/// <summary>
/// Side-on ocean cross-section: surface, left/right slopes, overhangs, caves, and a true seabed.
/// Collision is AABB on the XZ plane (Y is view depth).
/// </summary>
public sealed class OceanTerrain
{
	public const float SurfaceZ = 200f;
	public const float FloorZ = -980f;
	public const float MinX = -1500f;
	public const float MaxX = 1500f;

	public readonly List<TerrainSolid> Solids = new();
	public readonly List<GameObject> Visuals = new();
	public readonly List<Vector3> LedgePoints = new();
	public readonly List<Vector3> CavePoints = new();

	private Scene Scene;
	private readonly Random Rng;

	public OceanTerrain( Scene scene, int seed = 0 )
	{
		Scene = scene;
		Rng = seed == 0 ? new Random() : new Random( seed );
	}

	public void Build()
	{
		Clear();
		BuildSeabed();
		BuildLeftSlope();
		BuildRightSlope();
		BuildOverhangs();
		BuildCaves();
		BuildSurfaceShelf();
		SpawnVisuals();
	}

	public void Clear()
	{
		foreach ( var go in Visuals )
		{
			if ( go.IsValid() )
				go.Destroy();
		}

		Visuals.Clear();
		Solids.Clear();
		LedgePoints.Clear();
		CavePoints.Clear();
	}

	public Vector3 Resolve( Vector3 position, float radius )
	{
		var p = position;
		p.y = 0;

		// Soft world bounds
		p.x = Math.Clamp( p.x, MinX + radius, MaxX - radius );
		p.z = Math.Clamp( p.z, FloorZ + radius + 8f, SurfaceZ );

		for ( var pass = 0; pass < 3; pass++ )
		{
			foreach ( var solid in Solids )
			{
				if ( !Overlaps( p, radius, solid ) )
					continue;

				var dx = p.x - solid.Center.x;
				var dz = p.z - solid.Center.z;
				var px = solid.Half.x + radius;
				var pz = solid.Half.y + radius;
				var ox = px - MathF.Abs( dx );
				var oz = pz - MathF.Abs( dz );

				if ( ox < oz )
					p.x += MathF.Sign( dx == 0 ? 1 : dx ) * ox;
				else
					p.z += MathF.Sign( dz == 0 ? 1 : dz ) * oz;
			}
		}

		p.z = Math.Min( p.z, SurfaceZ );
		return p;
	}

	public bool IsOpen( Vector3 position, float radius = 18f )
	{
		var p = position.WithY( 0 );
		if ( p.x < MinX + 40 || p.x > MaxX - 40 )
			return false;
		if ( p.z < FloorZ + 40 || p.z > SurfaceZ - 10 )
			return false;

		foreach ( var solid in Solids )
		{
			if ( Overlaps( p, radius, solid ) )
				return false;
		}

		return true;
	}

	public Vector3 RandomOpenPoint( float minDepth, float maxDepth, float radius = 20f )
	{
		for ( var i = 0; i < 50; i++ )
		{
			var depth = minDepth + (float)Rng.NextDouble() * (maxDepth - minDepth);
			var x = MinX + 180 + (float)Rng.NextDouble() * (MaxX - MinX - 360);
			var z = SurfaceZ - depth * 2.2f;
			var p = new Vector3( x, 0, z );
			if ( IsOpen( p, radius ) )
				return p;
		}

		// Fallback toward center water column
		return new Vector3( 0, 0, SurfaceZ - Math.Clamp( minDepth, 20, 200 ) * 2.2f );
	}

	public bool AtSurface( Vector3 position ) => position.z >= SurfaceZ - 14f;

	public float DepthMeters( Vector3 position ) => MathF.Max( 0, (SurfaceZ - position.z) / 2.2f );

	private static bool Overlaps( Vector3 p, float radius, TerrainSolid solid )
	{
		var dx = MathF.Abs( p.x - solid.Center.x );
		var dz = MathF.Abs( p.z - solid.Center.z );
		return dx < solid.Half.x + radius && dz < solid.Half.y + radius;
	}

	private void AddSolid( float cx, float cz, float hx, float hz, string tag = "rock" )
	{
		Solids.Add( new TerrainSolid
		{
			Center = new Vector3( cx, 0, cz ),
			Half = new Vector2( hx, hz ),
			Tag = tag
		} );
	}

	private void BuildSeabed()
	{
		// Continuous floor with hills / trenches
		for ( var x = MinX; x < MaxX; x += 70f )
		{
			var hill = MathF.Sin( x * 0.007f ) * 55f + MathF.Sin( x * 0.019f ) * 30f;
			var top = FloorZ + 70f + hill;
			var mid = (FloorZ + top) * 0.5f;
			var halfH = (top - FloorZ) * 0.5f + 20f;
			AddSolid( x + 35f, mid - 10f, 40f, halfH, "floor" );
			LedgePoints.Add( new Vector3( x + 35f, 0, top + 18f ) );
		}

		// Deep trench pocket in the center-right
		AddSolid( 420f, FloorZ + 40f, 160f, 35f, "floor" );
	}

	private void BuildLeftSlope()
	{
		// Land / cliff on the left that slopes down into open water
		for ( var step = 0; step < 18; step++ )
		{
			var t = step / 17f;
			var x = MinX + 40f + t * 520f;
			var crest = SurfaceZ + 40f - t * 780f; // drops as we move right
			crest = MathF.Max( FloorZ + 120f, crest );
			var mid = (FloorZ + crest) * 0.5f;
			var halfH = (crest - FloorZ) * 0.5f;
			AddSolid( x, mid, 48f + (1f - t) * 40f, halfH + 10f, "wall" );
		}

		// Jagged inner face pockets
		for ( var i = 0; i < 7; i++ )
		{
			var z = SurfaceZ - 80f - i * 110f;
			var x = MinX + 260f + MathF.Sin( i * 1.7f ) * 40f;
			AddSolid( x, z, 55f, 34f, "wall" );
		}
	}

	private void BuildRightSlope()
	{
		for ( var step = 0; step < 18; step++ )
		{
			var t = step / 17f;
			var x = MaxX - 40f - t * 520f;
			var crest = SurfaceZ + 40f - t * 780f;
			crest = MathF.Max( FloorZ + 120f, crest );
			var mid = (FloorZ + crest) * 0.5f;
			var halfH = (crest - FloorZ) * 0.5f;
			AddSolid( x, mid, 48f + (1f - t) * 40f, halfH + 10f, "wall" );
		}

		for ( var i = 0; i < 7; i++ )
		{
			var z = SurfaceZ - 100f - i * 105f;
			var x = MaxX - 280f + MathF.Cos( i * 1.3f ) * 35f;
			AddSolid( x, z, 55f, 34f, "wall" );
		}
	}

	private void BuildOverhangs()
	{
		// Left overhangs
		AddSolid( -820f, SurfaceZ - 160f, 120f, 28f, "overhang" );
		AddSolid( -700f, SurfaceZ - 320f, 150f, 26f, "overhang" );
		AddSolid( -760f, SurfaceZ - 520f, 110f, 24f, "overhang" );
		AddSolid( -640f, SurfaceZ - 700f, 140f, 28f, "overhang" );
		LedgePoints.Add( new Vector3( -820f, 0, SurfaceZ - 130f ) );
		LedgePoints.Add( new Vector3( -700f, 0, SurfaceZ - 290f ) );
		LedgePoints.Add( new Vector3( -640f, 0, SurfaceZ - 670f ) );

		// Right overhangs
		AddSolid( 780f, SurfaceZ - 190f, 130f, 28f, "overhang" );
		AddSolid( 860f, SurfaceZ - 370f, 145f, 26f, "overhang" );
		AddSolid( 720f, SurfaceZ - 560f, 120f, 24f, "overhang" );
		AddSolid( 900f, SurfaceZ - 740f, 150f, 30f, "overhang" );
		LedgePoints.Add( new Vector3( 780f, 0, SurfaceZ - 160f ) );
		LedgePoints.Add( new Vector3( 860f, 0, SurfaceZ - 340f ) );
		LedgePoints.Add( new Vector3( 900f, 0, SurfaceZ - 710f ) );

		// Mid-ocean seamount with shelves
		AddSolid( 40f, FloorZ + 220f, 90f, 180f, "seamount" );
		AddSolid( 40f, FloorZ + 420f, 140f, 28f, "overhang" );
		AddSolid( -40f, FloorZ + 520f, 70f, 22f, "overhang" );
		LedgePoints.Add( new Vector3( 40f, 0, FloorZ + 455f ) );
	}

	private void BuildCaves()
	{
		// Left cave: corridor into a chamber. Solids form roof/floor/walls; interior stays open.
		var caveZ = SurfaceZ - 430f;
		AddSolid( -980f, caveZ + 70f, 160f, 22f, "cave" ); // roof
		AddSolid( -980f, caveZ - 70f, 160f, 22f, "cave" ); // floor
		AddSolid( -1120f, caveZ, 30f, 90f, "cave" ); // back wall
		// mouth pillars leave a swim gap near x=-860
		AddSolid( -860f, caveZ + 55f, 24f, 30f, "cave" );
		AddSolid( -860f, caveZ - 55f, 24f, 30f, "cave" );
		CavePoints.Add( new Vector3( -980f, 0, caveZ ) );
		CavePoints.Add( new Vector3( -1040f, 0, caveZ + 10f ) );
		LedgePoints.Add( new Vector3( -940f, 0, caveZ - 45f ) );

		// Deeper right cavern
		var deepZ = SurfaceZ - 720f;
		AddSolid( 980f, deepZ + 80f, 180f, 24f, "cave" );
		AddSolid( 980f, deepZ - 80f, 180f, 24f, "cave" );
		AddSolid( 1140f, deepZ, 28f, 100f, "cave" );
		AddSolid( 840f, deepZ + 50f, 26f, 34f, "cave" );
		AddSolid( 840f, deepZ - 50f, 26f, 34f, "cave" );
		CavePoints.Add( new Vector3( 980f, 0, deepZ ) );
		CavePoints.Add( new Vector3( 1040f, 0, deepZ - 10f ) );
		LedgePoints.Add( new Vector3( 960f, 0, deepZ - 52f ) );

		// Narrow mid cave under overhang
		var midZ = SurfaceZ - 250f;
		AddSolid( -200f, midZ + 45f, 200f, 18f, "cave" );
		AddSolid( -200f, midZ - 55f, 200f, 18f, "cave" );
		AddSolid( -380f, midZ, 22f, 55f, "cave" );
		CavePoints.Add( new Vector3( -120f, 0, midZ ) );
	}

	private void BuildSurfaceShelf()
	{
		// Shallow shelves near surface so the top of the dive feels like a coastline cross-section
		AddSolid( -1100f, SurfaceZ - 35f, 180f, 40f, "shelf" );
		AddSolid( 1100f, SurfaceZ - 35f, 180f, 40f, "shelf" );
		AddSolid( -400f, SurfaceZ - 25f, 90f, 18f, "shelf" );
		AddSolid( 350f, SurfaceZ - 28f, 100f, 18f, "shelf" );
	}

	private void SpawnVisuals()
	{
		foreach ( var solid in Solids )
		{
			var go = Scene.CreateObject();
			go.Name = $"Terrain_{solid.Tag}";
			go.WorldPosition = solid.Center + new Vector3( 0, 18f, 0 );

			var sprite = go.AddComponent<SpriteRenderer>();
			PixelActor.ConfigureRenderer( sprite );
			sprite.Size = new Vector2( solid.Half.x * 2.15f, solid.Half.y * 2.15f );

			var path = solid.Tag == "floor" ? "sprites/seabed.png" : "sprites/rock.png";
			var loaded = PixelActor.LoadSprite( path );
			if ( loaded is not null )
				sprite.Sprite = loaded;

			// The textures are already art-directed; only mild tints for depth mood.
			sprite.Color = solid.Tag switch
			{
				"floor" => Color.White,
				"overhang" => new Color( 1f, 0.88f, 0.78f ),
				"cave" => new Color( 0.62f, 0.70f, 0.85f ),
				"shelf" => new Color( 0.85f, 1f, 0.90f ),
				"seamount" => new Color( 0.80f, 0.88f, 0.95f ),
				_ => new Color( 0.85f, 0.92f, 1f )
			};

			Visuals.Add( go );
		}

		// Coral / detail on ledges
		foreach ( var ledge in LedgePoints )
		{
			for ( var i = 0; i < 3; i++ )
			{
				var go = Scene.CreateObject();
				go.Name = "TerrainDecor";
				go.WorldPosition = ledge + new Vector3( (i - 1) * 28f, 6f, 10f + i * 4f );
				var actor = go.AddComponent<PixelActor>();
				actor.Animation = i == 1 ? "coral" : "rock";
				actor.FrameCount = 1;
				actor.Size = i == 1 ? new Vector2( 40, 50 ) : new Vector2( 36, 28 );
				Visuals.Add( go );
			}
		}

		// Surface water line markers
		for ( var x = MinX; x < MaxX; x += 120f )
		{
			var go = Scene.CreateObject();
			go.Name = "SurfaceLine";
			go.WorldPosition = new Vector3( x + 60f, 4f, SurfaceZ + 2f );
			var sprite = go.AddComponent<SpriteRenderer>();
			PixelActor.ConfigureRenderer( sprite );
			sprite.Size = new Vector2( 130f, 10f );
			var bubble = PixelActor.LoadSprite( "sprites/bubble.png" );
			if ( bubble is not null )
				sprite.Sprite = bubble;
			sprite.Color = new Color( 0.55f, 0.85f, 1f, 0.55f );
			Visuals.Add( go );
		}
	}
}

public struct TerrainSolid
{
	public Vector3 Center;
	public Vector2 Half;
	public string Tag;
}
