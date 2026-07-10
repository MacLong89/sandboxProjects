namespace Terraingen.Player;

using Sandbox;

/// <summary>Finds the prefab FP camera rig and hides the local citizen body mesh.</summary>
public static class ThornsPlayerFirstPersonRig
{
	public const string ViewChildName = "View";

	public const float DefaultBodyHeight = 72f;

	public const float DefaultBodyRadius = 16f;

	public const float DefaultEyeOffsetZ = 52f;

	/// <summary>Stock player controller ships at 1.0; Thorns defaults to half that.</summary>
	public const float DefaultLookSensitivity = 0.5f;

	/// <summary>Camera / viewmodel anchor — prefers the enabled main camera under the pawn (stock <see cref="PlayerController"/>).</summary>
	public static GameObject ResolvePresentationCameraObject( GameObject player )
	{
		if ( player is null || !player.IsValid() )
			return default;

		if ( TryResolveActivePlayerCamera( player, out var activeCam ) && activeCam.GameObject.IsValid() )
			return activeCam.GameObject;

		var view = FindChildByName( player, ViewChildName );
		if ( !view.IsValid() )
			view = FindChildByName( player, "Camera" );

		if ( !view.IsValid() )
		{
			foreach ( var child in player.Children )
			{
				if ( !child.IsValid() )
					continue;

				if ( child.Components.Get<CameraComponent>().IsValid() )
				{
					view = child;
					break;
				}
			}
		}

		if ( !view.IsValid() )
		{
			view = new GameObject( true, ViewChildName );
			view.SetParent( player );
			view.LocalPosition = new Vector3( 0f, 0f, DefaultEyeOffsetZ );
			view.LocalRotation = Rotation.Identity;
			view.LocalScale = Vector3.One;
			_ = view.Components.Create<CameraComponent>();
		}

		return view;
	}

	/// <summary>Install pawn camera + viewmodel controller on the rig the player actually renders from.</summary>
	public static void EnsurePresentationComponents( GameObject player )
	{
		if ( player is null || !player.IsValid() )
			return;

		var rig = ResolvePresentationCameraObject( player );
		if ( !rig.IsValid() )
		{
			ThornsFpDebug.Write( "EnsurePresentationComponents: no camera rig on pawn." );
			return;
		}

		_ = rig.Components.Get<ThornsViewModelController>() ?? rig.Components.Create<ThornsViewModelController>();
		_ = rig.Components.Get<ThornsAdsSightController>() ?? rig.Components.Create<ThornsAdsSightController>();

		var pawnCam = rig.Components.Get<ThornsPawnCamera>() ?? rig.Components.Create<ThornsPawnCamera>();
		pawnCam.DriveCameraTransform = false;

		if ( ThornsLocalPlayer.IsLocallyControlledPawn( player ) )
		{
			var cam = rig.Components.Get<CameraComponent>();
			var sceneCam = player.Scene is { IsValid: true } s && s.Camera.IsValid() ? s.Camera : default;
			ThornsFpDebug.WriteOnce(
				$"rig-{player.Id}",
				$"rig='{rig.Name}' parent='{player.Name}' rigCam={( cam.IsValid() && cam.Enabled )} sceneMain={( sceneCam.IsValid() && sceneCam.IsMainCamera )} vmc={rig.Components.Get<ThornsViewModelController>().IsValid()}" );
		}
	}

	public static bool TryResolveActivePlayerCamera( GameObject player, out CameraComponent camera )
	{
		camera = default;
		if ( player is null || !player.IsValid() )
			return false;

		CameraComponent mainUnderPawn = null;
		CameraComponent anyUnderPawn = null;

		foreach ( var cam in player.Components.GetAll<CameraComponent>( FindMode.EverythingInDescendants ) )
		{
			if ( cam is null || !cam.IsValid() )
				continue;

			anyUnderPawn ??= cam;
			if ( !cam.Enabled )
				continue;

			if ( cam.IsMainCamera )
			{
				camera = cam;
				return true;
			}

			mainUnderPawn ??= cam;
		}

		if ( mainUnderPawn.IsValid() )
		{
			camera = mainUnderPawn;
			return true;
		}

		if ( anyUnderPawn.IsValid() )
		{
			camera = anyUnderPawn;
			return true;
		}

		var scene = player.Scene;
		if ( scene is not null && scene.IsValid && scene.Camera.IsValid() )
		{
			var sceneCamGo = scene.Camera.GameObject;
			if ( sceneCamGo.IsValid() && IsDescendantOf( player, sceneCamGo ) )
			{
				camera = scene.Camera;
				return true;
			}
		}

		return false;
	}

	public static void ApplyLocalOwnerPresentation( GameObject player )
	{
		if ( player is null || !player.IsValid() )
			return;

		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( player ) )
		{
			RestoreRemoteBodyMeshes( player );
			return;
		}

		HideLocalBodyMeshes( player );
	}

	/// <summary>Ensure the local pawn View is <see cref="CameraComponent.IsMainCamera"/> for mouse look.</summary>
	public static void EnsureLocalPresentationCamera( GameObject player )
	{
		if ( player is null || !player.IsValid() || !ThornsLocalPlayer.IsLocallyControlledPawn( player ) )
			return;

		var rig = ResolvePresentationCameraObject( player );
		if ( !rig.IsValid() )
			return;

		var pawnCam = rig.Components.Get<ThornsPawnCamera>();
		if ( pawnCam.IsValid() )
			pawnCam.EnsureStockMainCameraActive();
	}

	/// <summary>Drop death-camera world overrides so <see cref="PlayerController"/> owns the view rig again.</summary>
	public static void ReleaseDeathCameraPin( GameObject player )
	{
		if ( player is null || !player.IsValid() )
			return;

		var rig = FindChildByName( player, ViewChildName );
		if ( !rig.IsValid() )
			rig = ResolvePresentationCameraObject( player );
		if ( !rig.IsValid() )
			return;

		rig.LocalPosition = new Vector3( 0f, 0f, DefaultEyeOffsetZ );
		rig.LocalRotation = Rotation.Identity;
	}

	static void HideLocalBodyMeshes( GameObject player )
	{
		foreach ( var mr in player.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !mr.IsValid() )
				continue;

			if ( mr.GameObject == player )
				continue;

			if ( IsPresentationRigObject( player, mr.GameObject ) )
				continue;

			if ( mr.GameObject.Name.Equals( "Body", StringComparison.OrdinalIgnoreCase ) )
			{
				mr.Enabled = false;
				continue;
			}

			if ( mr.GameObject.Parent == player && mr is SkinnedModelRenderer )
				mr.Enabled = false;
		}
	}

	static void RestoreRemoteBodyMeshes( GameObject player )
	{
		foreach ( var mr in player.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !mr.IsValid() )
				continue;

			if ( IsPresentationRigObject( player, mr.GameObject ) )
				continue;

			if ( !mr.Enabled )
				mr.Enabled = true;
		}
	}

	static bool IsPresentationRigObject( GameObject player, GameObject go )
	{
		if ( go is null || !go.IsValid() || player is null || !player.IsValid() )
			return false;

		for ( var n = go; n.IsValid(); n = n.Parent )
		{
			if ( n == player )
				return false;

			if ( string.Equals( n.Name, ViewChildName, StringComparison.OrdinalIgnoreCase )
			     || string.Equals( n.Name, "Camera", StringComparison.OrdinalIgnoreCase ) )
				return true;

			if ( n.Components.Get<CameraComponent>().IsValid() )
				return true;
		}

		return false;
	}

	static bool IsDescendantOf( GameObject root, GameObject candidate )
	{
		if ( root is null || !root.IsValid() || candidate is null || !candidate.IsValid() )
			return false;

		for ( var n = candidate; n.IsValid(); n = n.Parent )
		{
			if ( n == root )
				return true;
		}

		return false;
	}

	static GameObject FindChildByName( GameObject root, string name )
	{
		foreach ( var c in root.Children )
		{
			if ( c.IsValid() && string.Equals( c.Name, name, StringComparison.OrdinalIgnoreCase ) )
				return c;
		}

		return default;
	}
}
