namespace Fauna2;

/// <summary>
/// Habitat interior floor — one shared depth for the whole enclosure in the
/// HabitatGroundLayer band (just above OwnedTile grass, well below enrichment).
/// Flat pads must NOT sit near camera Z (e.g. 1.5) or they depth-clip Nature props.
/// </summary>
public sealed class HabitatFloorDepthSorter : Component
{
	[Property] public GameObject SortOrigin { get; set; }

	private bool _frozen;

	protected override void OnStart() => ApplyDepth();

	protected override void OnUpdate()
	{
		if ( _frozen ) return;

		ApplyDepth();
		_frozen = true;
	}

	internal void ForceApplyDepth()
	{
		ApplyDepth();
		_frozen = true;
	}

	private void ApplyDepth()
	{
		var origin = SortOrigin.IsValid() ? SortOrigin : GameObject;
		var sortZ = PixelDepthSorter.SortZForHabitatFloor( origin.WorldPosition );

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
}
