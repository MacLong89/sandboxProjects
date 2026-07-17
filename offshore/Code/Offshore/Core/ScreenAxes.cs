namespace Offshore;

/// <summary>
/// Screen-facing axis helpers for our side-view camera.
/// Prefer camera basis vectors so placement matches the live view frustum:
/// <list type="bullet">
/// <item><b>Screen X</b> = camera Right</item>
/// <item><b>Screen Y</b> = camera Up</item>
/// <item><b>Depth</b> = camera Forward (into the scene)</item>
/// </list>
/// </summary>
public static class ScreenAxes
{
	public static Vector3 FromCamera( Component cam, float screenX, float screenY, float depthAlongForward )
	{
		var rot = cam.WorldRotation;
		return cam.WorldPosition
			+ rot.Right * screenX
			+ rot.Up * screenY
			+ rot.Forward * depthAlongForward;
	}

	public static void GetViewExtents( Component cam, float depthAlongForward, out float halfW, out float halfH )
	{
		var fov = cam.Components.Get<CameraComponent>()?.FieldOfView ?? OffshoreConstants.CamFov;
		var aspect = 16f / 9f;
		try
		{
			if ( Screen.Width > 0 && Screen.Height > 0 )
				aspect = (float)Screen.Width / Screen.Height;
		}
		catch { /* default */ }

		halfH = depthAlongForward * MathF.Tan( MathX.DegreeToRadian( fov * 0.5f ) );
		halfW = halfH * aspect;
	}
}
