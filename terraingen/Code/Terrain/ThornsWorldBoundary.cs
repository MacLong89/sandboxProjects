namespace Terraingen.TerrainGen;

using Terraingen;
using Terraingen.Core;
using Terraingen.Physics;
using Terraingen.Player;

/// <summary>Invisible static walls on the terrain footprint so players cannot walk or fall off the map.</summary>
public static class ThornsWorldBoundary
{
	const string BoundaryObjectName = "Thorns World Boundary";
	const float WallThicknessInches = 256f;
	const float WallHeightAboveMaxTerrainInches = 4096f;
	const float VoidFloorDepthInches = 512f;
	const float VoidFloorThicknessInches = 64f;

	public static GameObject Sync( Scene scene, GameObject terrainRoot, GameObject existing, Terrain terrain )
	{
		if ( !terrain.IsValid() )
		{
			if ( existing.IsValid() )
				existing.Destroy();
			return default;
		}

		var boundary = existing;
		if ( !boundary.IsValid() )
		{
			boundary = scene.CreateObject( true );
			boundary.Name = BoundaryObjectName;
			boundary.Parent = terrainRoot;
		}

		boundary.LocalPosition = Vector3.Zero;
		boundary.LocalRotation = Rotation.Identity;
		TerraingenAnchoredPhysics.EnsureSolidTags( boundary );
		boundary.Components.GetOrCreate<ThornsWorldBoundaryComponent>();

		var size = terrain.TerrainSize;
		var wallHeight = terrain.TerrainHeight + WallHeightAboveMaxTerrainInches;
		var centerZ = wallHeight * 0.5f;
		var halfThick = WallThicknessInches * 0.5f;
		var halfSpan = size * 0.5f;
		var span = size + WallThicknessInches * 2f;
		var cornerSpan = WallThicknessInches * 2f;

		EnsureWall( boundary, "wall-south", new Vector3( halfSpan, -halfThick, centerZ ), new Vector3( span, WallThicknessInches, wallHeight ) );
		EnsureWall( boundary, "wall-north", new Vector3( halfSpan, size + halfThick, centerZ ), new Vector3( span, WallThicknessInches, wallHeight ) );
		EnsureWall( boundary, "wall-west", new Vector3( -halfThick, halfSpan, centerZ ), new Vector3( WallThicknessInches, size, wallHeight ) );
		EnsureWall( boundary, "wall-east", new Vector3( size + halfThick, halfSpan, centerZ ), new Vector3( WallThicknessInches, size, wallHeight ) );

		EnsureWall( boundary, "wall-corner-sw", new Vector3( -halfThick, -halfThick, centerZ ), new Vector3( cornerSpan, cornerSpan, wallHeight ) );
		EnsureWall( boundary, "wall-corner-se", new Vector3( size + halfThick, -halfThick, centerZ ), new Vector3( cornerSpan, cornerSpan, wallHeight ) );
		EnsureWall( boundary, "wall-corner-nw", new Vector3( -halfThick, size + halfThick, centerZ ), new Vector3( cornerSpan, cornerSpan, wallHeight ) );
		EnsureWall( boundary, "wall-corner-ne", new Vector3( size + halfThick, size + halfThick, centerZ ), new Vector3( cornerSpan, cornerSpan, wallHeight ) );

		EnsureWall(
			boundary,
			"void-floor",
			new Vector3( halfSpan, halfSpan, -VoidFloorDepthInches ),
			new Vector3( span, span, VoidFloorThicknessInches ) );

		return boundary;
	}

	static void EnsureWall( GameObject root, string name, Vector3 localCenter, Vector3 scale )
	{
		GameObject wall = null;
		foreach ( var child in root.Children )
		{
			if ( child.IsValid() && child.Name == name )
			{
				wall = child;
				break;
			}
		}

		if ( !wall.IsValid() )
		{
			wall = root.Scene.CreateObject( true );
			wall.Name = name;
			wall.Parent = root;
		}

		wall.LocalPosition = localCenter;
		wall.LocalRotation = Rotation.Identity;
		TerraingenAnchoredPhysics.EnsureSolidTags( wall );

		foreach ( var renderer in wall.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelf ) )
			renderer.Destroy();

		var collider = wall.Components.GetOrCreate<BoxCollider>();
		collider.Center = Vector3.Zero;
		collider.Scale = scale;
		collider.IsTrigger = false;
		collider.Static = true;
		collider.Enabled = true;
	}
}

/// <summary>Keeps players inside map bounds when collision misses (high speed, net desync).</summary>
public sealed class ThornsWorldBoundaryComponent : Component
{
	protected override void OnUpdate()
	{
		ThornsPlayerRootCache.RefreshIfStale( Scene );

		foreach ( var root in ThornsPlayerRootCache.RootsReadOnly )
		{
			if ( !root.IsValid() )
				continue;

			var gameplay = root.Components.Get<ThornsPlayerGameplay>();
			if ( !gameplay.IsValid() )
				continue;

			var isLocal = gameplay.IsLocalPlayer();
			if ( !isLocal && !ThornsMultiplayer.IsHostOrOffline )
				continue;

			ThornsWorldBoundaryGuard.TryKeepPlayerOnMap( gameplay.GameObject );
		}
	}
}
