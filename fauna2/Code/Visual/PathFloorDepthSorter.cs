namespace Fauna2;

/// <summary>
/// Keeps path tiles in the path render band — above grass, under all other sprites.
/// </summary>
public sealed class PathFloorDepthSorter : Component
{
	private bool _frozen;

	protected override void OnStart() => ApplyDepth();

	protected override void OnUpdate()
	{
		if ( _frozen ) return;

		ApplyDepth();

		if ( !IsTrackingBuildGhost() )
			_frozen = true;
	}

	private void ApplyDepth()
	{
		var feet = GetPathFeet();
		var sortZ = PixelDepthSorter.SortZFor( feet, WorldSprites.PathLayer );

		if ( GameObject.Parent.IsValid() )
		{
			var localZ = sortZ - GameObject.Parent.WorldPosition.z;
			if ( MathF.Abs( GameObject.LocalPosition.z - localZ ) > 0.001f )
				GameObject.LocalPosition = GameObject.LocalPosition.WithZ( localZ );
		}
		else if ( MathF.Abs( GameObject.WorldPosition.z - sortZ ) > 0.001f )
		{
			GameObject.WorldPosition = GameObject.WorldPosition.WithZ( sortZ );
		}
	}

	private bool IsTrackingBuildGhost()
	{
		var node = GameObject;
		while ( node.Parent.IsValid() )
		{
			if ( node.Parent.Name == "Build Ghost" )
				return true;
			node = node.Parent;
		}

		return false;
	}

	private Vector3 GetPathFeet()
	{
		var node = GameObject;
		while ( node.Parent.IsValid() )
		{
			if ( node.Parent.Components.Get<PlaceableComponent>() is not null
				|| node.Parent.Name == "Build Ghost" )
				return node.Parent.WorldPosition.WithZ( 0f );

			node = node.Parent;
		}

		return GameObject.WorldPosition.WithZ( 0f );
	}
}
