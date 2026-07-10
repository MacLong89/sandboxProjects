namespace Fauna2;



/// <summary>
/// A placed decoration/path/nature/utility object. Networked; every machine
/// rebuilds pixel sprite visuals locally from the synced definition id.
/// </summary>

public sealed class PlaceableComponent : Component

{

	[Sync( SyncFlags.FromHost )] public string DefinitionId { get; set; } = "";



	public PlaceableDefinition Definition => _definition ??= Defs.Placeable( DefinitionId );



	private PlaceableDefinition _definition;

	private GameObject _visualRoot;



	/// <summary>Host spawn path — bind the fully-loaded definition before networking.</summary>

	public void Initialize( PlaceableDefinition def, float restaurantUncollected = 0f )

	{

		_definition = def;

		DefinitionId = Defs.IdOf( def );

		RebuildVisuals();

		EnsureCollectibleComponent( restaurantUncollected );
		EnsureFootprintCollider();

		if ( Networking.IsHost )
			PlaceableRegistry.Register( this );
	}



	private void EnsureCollectibleComponent( float uncollected = 0f )

	{

		if ( Definition?.ProvidesCollectibleRevenue != true )

			return;



		var restaurant = Components.Get<RestaurantComponent>() ?? Components.Create<RestaurantComponent>();

		if ( uncollected > 0f )

			restaurant.Uncollected = uncollected;

	}



	private void EnsureFootprintCollider()

	{

		if ( Definition?.IsPathTile == true )

			return;



		if ( Components.Get<BoxCollider>() is not null )

			return;



		var footprint = Definition.EffectiveFootprint;

		var collider = Components.Create<BoxCollider>();

		collider.Scale = new Vector3( footprint.x, footprint.y, 96f );

		collider.Center = new Vector3( 0, 0, 48f );

		collider.Static = true;

		GameObject.Tags.Add( "walk_block" );

	}



	protected override void OnStart()

	{

		RebuildVisuals();

		if ( !PlaceableRegistry.All.Contains( this ) )
			PlaceableRegistry.Register( this );

		EnsureCollectibleComponent();
		EnsureFootprintCollider();

		if ( Definition?.IsPathTile == true )
			AmbientGuests.NudgeFromPathTile( GameObject.WorldPosition );

	}



	protected override void OnDestroy()

	{

		PlaceableRegistry.Unregister( this );

		_visualRoot?.Destroy();

	}



	private void RebuildVisuals()

	{

		var def = Definition;

		if ( def is null ) return;



		_visualRoot?.Destroy();

		if ( def.IsPathTile )
		{
			_visualRoot = new GameObject( GameObject, true, "Visuals" );
			PathGroundOverlay.Attach(
				_visualRoot,
				def,
				parentWorldZ: GameObject.WorldPosition.z,
				worldPosition: GameObject.WorldPosition );
			return;
		}

		_visualRoot = BuildVisualTree( def, GameObject );

	}



	/// <summary>
	/// Builds a definition's pixel sprite under a parent. Shared with the build ghost.
	/// </summary>

	public static GameObject BuildVisualTree( PlaceableDefinition def, GameObject parent, float alpha = 1f, Color? tintOverride = null, bool dynamicDepthSort = false )

	{

		var root = new GameObject( parent, true, "Visuals" );

		if ( def is null ) return root;



		var size = WorldSpriteCatalog.SizeFor( def );

		var propKey = WorldSpriteCatalog.PropFor( def );

		if ( propKey == "habitat_ground" )
		{
			HabitatGroundOverlay.Attach( root, def.HabitatSize, def.HabitatBiome, alpha );
			return root;
		}

		Texture texture;
		if ( !PixelArt.TryProp( propKey, out texture ) )
		{
			Fauna2Debug.Warn( "Assets", $"Placeable '{Defs.IdOf( def )}' uses missing prop '{propKey}' — skipped visual" );
			return root;
		}

		var drawSize = WorldSpriteCatalog.UsesFootprintDrawDimensions( def, propKey )
			? WorldSpriteCatalog.DrawDimensionsFor( def )
			: (Vector2?)null;



		WorldSprites.Spawn(
			root,
			texture,
			size,
			new Vector3( 0, 0, 1f ),
			propKey,
			depthSort: true,
			dynamicDepthSort: dynamicDepthSort,
			layer: WorldSprites.SortLayerFor( def ),
			sourcePixels: PixelArt.PropSourcePixels( propKey ),
			drawSize: drawSize );

		if ( alpha < 0.99f )
		{
			foreach ( var renderer in root.GetComponentsInChildren<SpriteRenderer>() )
			{
				if ( renderer.IsValid() )
					renderer.Color = renderer.Color.WithAlpha( alpha );
			}
		}

		return root;

	}

}

