namespace Sandbox;

using Sandbox.Rendering;

/// <summary>
/// In-game optic ADS tuning. Press [ with a supported sight equipped to lock ADS and nudge the viewmodel.
/// I/J/K/L move on screen, ,/. depth, U reset, P copy values into console.
/// </summary>
public static class AimboxOpticAdsTuner
{
	public const float MoveStep = 0.05f;
	public const float FineMoveStep = 0.01f;

	public static bool TunerEnabled { get; set; } = true;
	public static bool IsActive { get; private set; }

	public static bool SupportsWeapon( AimboxPlayerController player ) =>
		AimboxOpticAdsLayout.ResolveTuningTarget( player ) != OpticAdsTuningTarget.None;

	public static void Tick( AimboxPlayerController player )
	{
		if ( !TunerEnabled || player is null || player.IsProxy )
			return;

		if ( !SupportsWeapon( player ) )
		{
			IsActive = false;
			return;
		}

		if ( KeyPressed( "[" ) )
		{
			IsActive = !IsActive;
			if ( IsActive )
			{
				var target = AimboxOpticAdsLayout.DescribeTarget( AimboxOpticAdsLayout.ResolveTuningTarget( player ) );
				Log.Info( $"[Aimbox Optic ADS] Tuner ON — ADS locked. Target: {target}. I/J/K/L move mesh, ,/. depth, P copy, U reset, [ exit." );
			}
			else
				Log.Info( "[Aimbox Optic ADS] Tuner OFF." );
		}

		if ( !IsActive )
			return;

		var tuningTarget = AimboxOpticAdsLayout.ResolveTuningTarget( player );
		var fine = AimboxOpticAdsLayout.GetActiveFineTune( tuningTarget );

		var step = Input.Down( "Run" ) ? FineMoveStep : MoveStep;

		// Viewmodel-local: +Z raises optic on screen, +Y moves left, +X moves forward.
		if ( KeyDown( "k" ) )
			fine.z += step;
		if ( KeyDown( "i" ) )
			fine.z -= step;
		if ( KeyDown( "j" ) )
			fine.y += step;
		if ( KeyDown( "l" ) )
			fine.y -= step;
		if ( KeyDown( "," ) )
			fine.x -= step;
		if ( KeyDown( "." ) )
			fine.x += step;

		AimboxOpticAdsLayout.SetActiveFineTune( tuningTarget, fine );

		if ( KeyPressed( "u" ) )
		{
			AimboxOpticAdsLayout.ResetActiveTuning( player );
			SyncAttachmentTuner( player );
		}

		if ( KeyPressed( "p" ) )
		{
			Log.Info( "[Aimbox Optic ADS] Paste into AimboxAdsSightTuning.cs:\n" + AimboxOpticAdsLayout.FormatForCopyPaste( player ) );
			SyncAttachmentTuner( player );
		}
	}

	public static void DrawHud( CameraComponent camera, AimboxPlayerController player )
	{
		if ( !IsActive || camera is null || !camera.IsValid() || player is null )
			return;

		var target = AimboxOpticAdsLayout.ResolveTuningTarget( player );
		var fine = AimboxOpticAdsLayout.GetActiveFineTune( target );

		var hud = camera.Hud;
		var y = 148f;
		DrawHelpLine( hud, ref y, "Optic ADS tuner ([ exit, ADS locked)" );
		DrawHelpLine( hud, ref y, $"Target: {AimboxOpticAdsLayout.DescribeTarget( target )}" );
		DrawHelpLine( hud, ref y, "Screen: I/K up/down  J/L left/right  |  Depth: , .  |  Fine: Shift" );
		DrawHelpLine( hud, ref y, "Reset: U  |  Copy values: P" );
		DrawHelpLine( hud, ref y, $"fineTune=({fine.x:F2}, {fine.y:F2}, {fine.z:F2})" );
	}

	static bool KeyPressed( string key ) => Input.Keyboard.Pressed( key );
	static bool KeyDown( string key ) => Input.Keyboard.Down( key );

	static void SyncAttachmentTuner( AimboxPlayerController player )
	{
		var tuner = AimboxViewModelAttachmentTuner.Instance;
		if ( tuner is null || player.ActiveWeapon != AimboxWeaponId.M700 )
			return;

		tuner.M700ScopeAdsFineTune = Vector3.Zero;
	}

	static void DrawHelpLine( HudPainter hud, ref float y, string text )
	{
		hud.DrawText(
			new TextRendering.Scope( text, Color.White.WithAlpha( 0.92f ), 16 ),
			new Vector2( 24f, y ) );
		y += 22f;
	}
}

/// <summary>Draws optic mesh ADS tuner HUD on the player camera.</summary>
[Title( "Aimbox Optic ADS Tuner HUD" )]
[Category( "Aimbox/Debug" )]
public sealed class AimboxOpticAdsTunerHud : Component
{
	protected override void OnPreRender()
	{
		if ( IsProxy )
			return;

		var player = Components.GetInParent<AimboxPlayerController>();
		AimboxOpticAdsTuner.DrawHud( Components.Get<CameraComponent>(), player );
	}
}
