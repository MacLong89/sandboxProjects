namespace Fauna2;

/// <summary>
/// Keeps billboard sprites ordered like a top-down 2D game: objects closer to the bottom
/// of the screen (higher world Y) draw on top of objects further away.
/// </summary>
public sealed class PixelDepthSorter : Component
{
	[Property] public float BaseLayer { get; set; } = WorldSprites.BuildingLayer;
	/// <summary>Optional override for the world XY used to pick sort depth (Z is ignored).</summary>
	[Property] public GameObject SortOrigin { get; set; }
	/// <summary>Preserved for scene compatibility — feet alignment uses local Z on the sprite.</summary>
	[Property] public float FeetLiftZ { get; set; }
	/// <summary>When false, depth is computed once at spawn (trees, buildings, placed paths).</summary>
	[Property] public bool Dynamic { get; set; }

	private const float SortMoveThresholdSq = 12f * 12f;

	private bool _applied;
	private Vector3 _lastSortFeet;

	protected override void OnStart()
	{
		_lastSortFeet = GetSortFeet();
		ApplyDepth();
	}

	protected override void OnUpdate()
	{
		if ( !Dynamic )
		{
			if ( _applied ) return;
			ApplyDepth();
			_applied = true;
			return;
		}

		var feet = GetSortFeet();
		if ( (feet - _lastSortFeet).LengthSquared < SortMoveThresholdSq )
			return;

		_lastSortFeet = feet;
		ApplyDepth();
	}

	internal void ForceApplyDepth()
	{
		_lastSortFeet = GetSortFeet();
		ApplyDepth();
		_applied = true;
	}

	private void ApplyDepth()
	{
		var sortFeet = GetSortFeet();
		var sortZ = SortZFor( sortFeet, BaseLayer );

		if ( GameObject.Parent.IsValid() )
		{
			var parentZ = GameObject.Parent.WorldPosition.z;
			var localZ = sortZ - parentZ;
			if ( MathF.Abs( GameObject.LocalPosition.z - localZ ) > 0.001f )
				GameObject.LocalPosition = GameObject.LocalPosition.WithZ( localZ );
		}
		else if ( MathF.Abs( GameObject.WorldPosition.z - sortZ ) > 0.001f )
		{
			GameObject.WorldPosition = GameObject.WorldPosition.WithZ( sortZ );
		}
	}

	private Vector3 GetSortFeet()
	{
		if ( SortOrigin.IsValid() && SortOrigin != GameObject )
			return SortOrigin.WorldPosition.WithZ( 0f );

		return GameObject.WorldPosition.WithZ( 0f );
	}

	public static float SortZFor( Vector3 feet, float baseLayer )
	{
		var half = GameConstants.PlayableHalfExtent;
		var t = ((feet.y + half) / (half * 2f)).Clamp( 0f, 1f );
		return baseLayer + (t - 0.5f) * WorldSprites.YSortSpread;
	}

	public static float SortZForPath( Vector3 feet ) =>
		SortZFor( feet, WorldSprites.PathLayer );
}
