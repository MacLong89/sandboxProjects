namespace Deep;

/// <summary>Maps the mouse cursor to a point on the dive play plane (Y = 0).</summary>
public static class DivePointer
{
	public static bool TryGetPlayPlanePoint( out Vector3 worldPoint )
	{
		worldPoint = default;

		var camera = DeepGame.Instance?.DiveCamera?.Camera;
		if ( camera is null || !camera.IsValid() )
			return false;

		var ray = camera.ScreenPixelToRay( Mouse.Position );

		// Side-view camera on +Y; intersect the XZ play plane at Y = 0.
		if ( MathF.Abs( ray.Forward.y ) < 0.0001f )
			return false;

		var t = -ray.Position.y / ray.Forward.y;
		if ( t < 0f )
			return false;

		worldPoint = ray.Position + ray.Forward * t;
		worldPoint.y = 0f;
		return true;
	}
}
