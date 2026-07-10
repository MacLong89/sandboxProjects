namespace Sandbox;

using Sandbox.Rendering;

/// <summary>
/// In-game scope PiP tuning. Press O with ranged sight equipped to lock ADS and nudge the circle.
/// </summary>
public static class AimboxM700ScopePipTuner
{
	public const float MoveStep = 4f;
	public const float FineMoveStep = 1f;
	public const float RadiusStep = 0.02f;

	public static bool TunerEnabled { get; set; } = true;
	public static bool IsActive { get; private set; }

	public static bool SupportsPlayer( AimboxPlayerController player ) =>
		AimboxM700ScopePipLayout.SupportsPlayer( player );

	public static void Tick( AimboxPlayerController player )
	{
		if ( !TunerEnabled || player is null || player.IsProxy )
			return;

		if ( !SupportsPlayer( player ) )
		{
			IsActive = false;
			return;
		}

		if ( KeyPressed( "o" ) )
		{
			IsActive = !IsActive;
			if ( IsActive )
			{
				AimboxM700ScopePipLayout.SetActiveTarget( AimboxM700ScopePipLayout.ResolveTarget( player ) );
				Log.Info(
					$"[Aimbox Scope PiP] Tuner ON — {AimboxM700ScopePipLayout.DescribeActiveTarget()}. " +
					"ADS locked. I/J/K/L move, ,/. size, P copy, U reset, O exit." );
			}
			else
				Log.Info( "[Aimbox Scope PiP] Tuner OFF." );
		}

		if ( !IsActive )
			return;

		var expectedTarget = AimboxM700ScopePipLayout.ResolveTarget( player );
		if ( expectedTarget != AimboxM700ScopePipLayout.ActiveTarget )
			AimboxM700ScopePipLayout.SetActiveTarget( expectedTarget );

		var step = Input.Down( "Run" ) ? FineMoveStep : MoveStep;
		var offset = AimboxM700ScopePipLayout.PanelOffset;

		if ( KeyDown( "i" ) )
			offset.y -= step;
		if ( KeyDown( "k" ) )
			offset.y += step;
		if ( KeyDown( "j" ) )
			offset.x -= step;
		if ( KeyDown( "l" ) )
			offset.x += step;

		AimboxM700ScopePipLayout.PanelOffset = offset;

		if ( KeyPressed( "," ) )
			AimboxM700ScopePipLayout.RadiusScale = MathF.Max( 0.2f, AimboxM700ScopePipLayout.RadiusScale - RadiusStep );
		if ( KeyPressed( "." ) )
			AimboxM700ScopePipLayout.RadiusScale += RadiusStep;

		if ( KeyPressed( "u" ) )
			AimboxM700ScopePipLayout.ResetToDefaults();

		if ( KeyPressed( "p" ) )
		{
			Log.Info( "[Aimbox Scope PiP] Paste into AimboxAdsSightTuning.cs:\n" + AimboxM700ScopePipLayout.FormatForCopyPaste() );
			SyncAttachmentTuner();
		}
	}

	public static void DrawHud( CameraComponent camera )
	{
		if ( !IsActive || camera is null || !camera.IsValid() )
			return;

		var hud = camera.Hud;
		var y = 72f;
		DrawHelpLine( hud, ref y, $"Scope PiP Tuner — {AimboxM700ScopePipLayout.DescribeActiveTarget()} (O exit, ADS locked)" );
		DrawHelpLine( hud, ref y, "Move: I/J/K/L  |  Fine: Shift+I/J/K/L  |  Radius: , ." );
		DrawHelpLine( hud, ref y, "Reset: U  |  Copy values: P" );
		DrawHelpLine( hud, ref y,
			$"offset=({AimboxM700ScopePipLayout.PanelOffset.x:F1}, {AimboxM700ScopePipLayout.PanelOffset.y:F1}) " +
			$"radiusScale={AimboxM700ScopePipLayout.RadiusScale:F3}" );
	}

	static bool KeyPressed( string key ) => Input.Keyboard.Pressed( key );
	static bool KeyDown( string key ) => Input.Keyboard.Down( key );

	static void SyncAttachmentTuner()
	{
		var tuner = AimboxViewModelAttachmentTuner.Instance;
		if ( tuner is null )
			return;

		tuner.M700ScopePipOffsetX = AimboxM700ScopePipLayout.PanelOffset.x;
		tuner.M700ScopePipOffsetY = AimboxM700ScopePipLayout.PanelOffset.y;
		tuner.M700ScopePipRadiusScale = AimboxM700ScopePipLayout.RadiusScale;
	}

	static void DrawHelpLine( HudPainter hud, ref float y, string text )
	{
		hud.DrawText(
			new TextRendering.Scope( text, Color.White.WithAlpha( 0.92f ), 16 ),
			new Vector2( 24f, y ) );
		y += 22f;
	}
}

/// <summary>Draws scope PiP tuner HUD on the player camera.</summary>
[Title( "Aimbox M700 Scope PiP Tuner HUD" )]
[Category( "Aimbox/Debug" )]
public sealed class AimboxM700ScopePipTunerHud : Component
{
	protected override void OnPreRender()
	{
		if ( IsProxy )
			return;

		AimboxM700ScopePipTuner.DrawHud( Components.Get<CameraComponent>() );
	}
}
