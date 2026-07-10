namespace Terraingen.UI;

/// <summary>Scene-level hook for combat hit feedback timing (crosshair flash on menu HUD).</summary>
[Title( "Thorns Combat Feedback" )]
[Category( "Thorns/UI" )]
[Icon( "gps_fixed" )]
public sealed class ThornsCombatFeedbackHost : Component
{
	public static ThornsCombatFeedbackHost Instance { get; private set; }

	protected override void OnStart()
	{
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		ThornsHitmarkerState.Reset();
		ThornsDamageFlashState.Reset();
		ThornsUnderwaterViewState.Reset();
		Instance = this;
		EnsureFeedbackHud();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void EnsureFeedbackHud()
	{
		// Hitmarker UI is drawn on ThornsMenuHost's ScreenPanel — avoid a second PanelComponent here.
	}

	/// <summary>Attach combat HUD to bootstrap / terrain root if the scene omits the host component.</summary>
	public static void EnsureOn( GameObject host )
	{
		if ( !host.IsValid() )
			return;

		var existing = host.Components.Get<ThornsCombatFeedbackHost>( FindMode.EnabledInSelf );
		if ( existing.IsValid() )
		{
			existing.EnsureFeedbackHud();
			return;
		}

		host.Components.Create<ThornsCombatFeedbackHost>();
	}
}
