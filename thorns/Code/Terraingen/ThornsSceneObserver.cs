namespace Terraingen;

/// <summary>
/// Cached scene observer (Terrain Explorer + main camera) for foliage, clutter, and HUD.
/// </summary>
public static class ThornsSceneObserver
{
	public static Vector3 Resolve(
		Scene scene,
		ref GameObject explorerObject,
		ref CameraComponent mainCamera,
		ref TimeUntil nextRefresh )
	{
		if ( nextRefresh || (!IsValid( explorerObject ) && !IsValid( mainCamera )) )
			Refresh( scene, ref explorerObject, ref mainCamera, ref nextRefresh );

		if ( IsValid( explorerObject ) )
			return explorerObject.WorldPosition;

		if ( IsValid( mainCamera ) )
			return mainCamera.GameObject.WorldPosition;

		return Vector3.Zero;
	}

	public static void Refresh(
		Scene scene,
		ref GameObject explorerObject,
		ref CameraComponent mainCamera,
		ref TimeUntil nextRefresh )
	{
		nextRefresh = 1f;

		if ( !IsValid( explorerObject ) )
		{
			foreach ( var obj in scene.GetAllObjects( true ) )
			{
				if ( obj.Name.Equals( "Terrain Explorer", StringComparison.OrdinalIgnoreCase ) && obj.IsValid() )
				{
					explorerObject = obj;
					break;
				}
			}
		}

		if ( !IsValid( mainCamera ) )
		{
			foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
			{
				if ( cam.IsValid() && cam.IsMainCamera )
				{
					mainCamera = cam;
					break;
				}
			}
		}
	}

	static bool IsValid( GameObject obj ) => obj is not null && obj.IsValid();

	static bool IsValid( CameraComponent cam ) => cam is not null && cam.IsValid();
}
