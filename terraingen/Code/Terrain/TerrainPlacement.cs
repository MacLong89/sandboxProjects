namespace Terraingen.TerrainGen;

/// <summary>
/// s&box terrain extends from local (0,0) to (TerrainSize, TerrainSize). Center it on the world origin.
/// </summary>
public static class TerrainPlacement
{
	public static void ApplyOriginOffset( GameObject terrainRoot, float terrainWorldSize, bool centerAtOrigin )
	{
		terrainRoot.WorldRotation = Rotation.Identity;

		if ( !centerAtOrigin )
			return;

		var half = terrainWorldSize * 0.5f;
		terrainRoot.WorldPosition = new Vector3( -half, -half, 0f );
	}

	public static void FramePreviewCamera( Scene scene, float terrainWorldSize, float maxHeight )
	{
		CameraComponent target = null;

		foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
		{
			if ( !cam.IsValid() )
				continue;

			if ( cam.IsMainCamera )
			{
				target = cam;
				break;
			}

			target ??= cam;
		}

		if ( target is null || !target.GameObject.IsValid() )
			return;

		var distance = terrainWorldSize * 0.38f;
		var height = maxHeight * 0.35f + terrainWorldSize * 0.08f;

		target.GameObject.WorldPosition = new Vector3( 0f, -distance, height );
		target.GameObject.WorldRotation = new Angles( 32f, 0f, 0f ).ToRotation();
	}
}
