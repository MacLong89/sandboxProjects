namespace Terraingen.Animals;

/// <summary>Wildlife .vmdl bone attachments must never own the gameplay main camera.</summary>
static class ThornsAnimalCameraGuard
{
	public static void ConfigureRenderer( SkinnedModelRenderer renderer )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		renderer.CreateBoneObjects = false;
		renderer.UseAnimGraph = false;
	}

	public static void SuppressStrayCameras( GameObject root )
	{
		if ( root is null || !root.IsValid() )
			return;

		foreach ( var cam in root.Components.GetAll<CameraComponent>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( cam is null || !cam.IsValid() )
				continue;

			if ( IsPlayerOwnedCamera( cam.GameObject ) )
				continue;

			cam.Enabled = false;
			cam.IsMainCamera = false;
		}
	}

	public static bool IsWildlifeCamera( GameObject cameraObject )
	{
		if ( cameraObject is null || !cameraObject.IsValid() )
			return false;

		if ( IsPlayerOwnedCamera( cameraObject ) )
			return false;

		for ( var node = cameraObject; node.IsValid(); node = node.Parent )
		{
			if ( node.Tags.Has( "animal" ) )
				return true;
		}

		return false;
	}

	/// <summary>Mounted riders parent under wildlife — their cameras must stay enabled.</summary>
	public static bool IsPlayerOwnedCamera( GameObject cameraObject )
	{
		if ( cameraObject is null || !cameraObject.IsValid() )
			return false;

		for ( var node = cameraObject; node.IsValid(); node = node.Parent )
		{
			if ( node.Components.Get<PlayerController>( FindMode.EverythingInSelf ) is { IsValid: true } )
				return true;
		}

		return false;
	}

	/// <summary>Disable every camera under wildlife — bone attachments can appear after spawn.</summary>
	public static int SuppressWildlifeCamerasInScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return 0;

		var suppressed = 0;

		foreach ( var cam in scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam is null || !cam.IsValid() || !cam.GameObject.IsValid() )
				continue;

			if ( !IsWildlifeCamera( cam.GameObject ) )
				continue;

			if ( !cam.Enabled && !cam.IsMainCamera )
				continue;

			cam.Enabled = false;
			cam.IsMainCamera = false;
			suppressed++;
		}

		return suppressed;
	}
}

/// <summary>Continuously strips bone cameras from one animal instance.</summary>
[Title( "Thorns Animal Camera Guard" )]
[Category( "Thorns/Animals" )]
public sealed class ThornsAnimalCameraGuardHost : Component
{
	ThornsAnimalVisual _visual;

	protected override void OnStart()
	{
		_visual = Components.Get<ThornsAnimalVisual>( FindMode.EverythingInSelfAndParent );
		ThornsAnimalCameraGuard.SuppressStrayCameras( GameObject );
	}

	protected override void OnUpdate()
	{
		if ( _visual?.Renderer is { IsValid: true } renderer )
			ThornsAnimalCameraGuard.ConfigureRenderer( renderer );

		ThornsAnimalCameraGuard.SuppressStrayCameras( GameObject );
	}
}
