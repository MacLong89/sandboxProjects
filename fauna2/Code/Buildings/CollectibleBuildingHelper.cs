namespace Fauna2;

/// <summary>Shared helpers for restaurant/shop tip badges and proximity collection.</summary>
public static class CollectibleBuildingHelper
{
	public static float CollectibleInteractRange => GameConstants.InteractionRange + GameConstants.TileSize;

	public static string TipKey( PlaceableComponent placeable )
	{
		if ( placeable is null || !placeable.IsValid() )
			return "";

		var pos = placeable.GameObject.WorldPosition;
		return $"{placeable.DefinitionId}_{(int)pos.x}_{(int)pos.y}_{placeable.GameObject.Id}";
	}

	public static PlaceableComponent FindByTipKey( string key )
	{
		if ( string.IsNullOrEmpty( key ) )
			return null;

		foreach ( var placeable in PlaceableRegistry.All )
		{
			if ( !placeable.IsValid() )
				continue;

			if ( TipKey( placeable ) == key )
				return placeable;
		}

		return null;
	}

	/// <summary>XY distance from a point to the nearest edge of the building footprint.</summary>
	public static float DistanceToFootprint( Vector3 point, PlaceableComponent placeable )
	{
		if ( placeable is null || !placeable.IsValid() )
			return float.MaxValue;

		var footprint = WorldSpriteCatalog.DrawDimensionsFor( placeable.Definition );
		var center = placeable.GameObject.WorldPosition;
		var halfX = footprint.x * 0.5f;
		var halfY = footprint.y * 0.5f;

		var dx = MathF.Max( MathF.Abs( point.x - center.x ) - halfX, 0f );
		var dy = MathF.Max( MathF.Abs( point.y - center.y ) - halfY, 0f );
		return MathF.Sqrt( dx * dx + dy * dy );
	}

	public static bool IsWithinCollectRange( Vector3 feet, PlaceableComponent placeable ) =>
		DistanceToFootprint( feet, placeable ) <= CollectibleInteractRange;

	/// <summary>World point above the building sprite — used for floating tip badges.</summary>
	public static Vector3 TipAnchorWorld( PlaceableComponent placeable )
	{
		if ( placeable is null || !placeable.IsValid() )
			return Vector3.Zero;

		var def = placeable.Definition;
		var root = placeable.GameObject;

		if ( def is not null )
		{
			var footprint = def.EffectiveFootprint;
			var propKey = WorldSpriteCatalog.PropFor( def );
			if ( PixelArt.TryGetBuildingTipAnchorLocal( propKey, footprint, out var localAnchor ) )
			{
				var anchor = root.WorldPosition + root.WorldRotation * localAnchor;
				foreach ( var renderer in root.GetComponentsInChildren<SpriteRenderer>( true ) )
				{
					if ( !renderer.IsValid() || renderer.Size.y <= 8f )
						continue;

					var pivot = renderer.GameObject.WorldPosition;
					return new Vector3( pivot.x, pivot.y, anchor.z );
				}

				return anchor;
			}
		}

		foreach ( var renderer in root.GetComponentsInChildren<SpriteRenderer>( true ) )
		{
			if ( !renderer.IsValid() || renderer.Size.y <= 8f )
				continue;

			var pivot = renderer.GameObject.WorldPosition;
			return pivot.WithZ( pivot.z + renderer.Size.y * 0.52f );
		}

		if ( def is null )
			return root.WorldPosition;

		var draw = WorldSpriteCatalog.DrawDimensionsFor( def );
		var height = MathF.Max( draw.x, draw.y );
		return root.WorldPosition.WithZ( height * 0.42f + 24f );
	}

	public static bool TryCollect( PlaceableComponent placeable )
	{
		var restaurant = placeable?.Components.Get<RestaurantComponent>();
		if ( restaurant is null || restaurant.Uncollected < 1f )
			return false;

		restaurant.RequestCollect();
		return true;
	}

	public static bool TryCollectAtMouse( Scene scene )
	{
		var restaurant = PlaceableRegistry.PickCollectibleAtMouse( scene );
		if ( restaurant is null || restaurant.Uncollected < 1f )
			return false;

		restaurant.RequestCollect();
		return true;
	}
}
