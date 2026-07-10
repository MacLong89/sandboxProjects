namespace Terraingen.UI.Menu;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Main menu view of the same <see cref="ThornsLocalSettings"/> as in-game.</summary>
public sealed class MainMenuSettingsScreen : Panel
{
	public event Action MenuBackPressed;

	public MainMenuSettingsScreen( Panel parent )
	{
		parent.AddChild( this );
		AddClass( "mainmenu-overlay-screen mainmenu-settings-screen" );
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.Padding = Length.Pixels( 24 );
		Style.MinHeight = 0;

		var head = ThornsUiFactory.AddPanel( this, "mainmenu-overlay-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		ThornsUiFactory.AddLabel( head, "SETTINGS", "mainmenu-overlay-title" );
		ThornsUiFactory.AddClickable( head, "mainmenu-back", "← Back", () => MenuBackPressed?.Invoke() );

		var body = ThornsUiFactory.AddPanel( this, "settings-body thorns-glass" );
		ThornsTheme.ApplyGlassPanel( body );
		body.Style.Padding = Length.Pixels( 24 );
		body.Style.FlexGrow = 1;
		body.Style.FlexDirection = FlexDirection.Column;
		body.Style.Overflow = OverflowMode.Scroll;

		AddStepper( body, "UI SCALE", () => ThornsLocalSettings.Current.UiScale, v =>
		{
			ThornsLocalSettings.Current.UiScale = Math.Clamp( v, 0.75f, 1.5f );
			ThornsLocalSettings.Save();
		}, 0.05f );

		AddSkinToggle( body );

		AddStepper( body, "MASTER VOLUME", () => ThornsLocalSettings.Current.MasterVolume, v =>
		{
			ThornsLocalSettings.Current.MasterVolume = Math.Clamp( v, 0f, 1f );
			ThornsLocalSettings.Save();
		}, 0.05f );

		AddStepper( body, "CROSSHAIR SCALE", () => ThornsLocalSettings.Current.CrosshairScale, v =>
		{
			ThornsLocalSettings.Current.CrosshairScale = Math.Clamp( v, 0.5f, 2f );
			ThornsLocalSettings.Save();
		}, 0.1f );

		AddToggleButton( body, "VSYNC", () => ThornsLocalSettings.Current.Vsync, v =>
		{
			ThornsLocalSettings.Current.Vsync = v;
			ThornsLocalSettings.Save();
		} );

		AddToggleButton( body, "COLORBLIND MODE", () => ThornsLocalSettings.Current.ColorblindMode, v =>
		{
			ThornsLocalSettings.Current.ColorblindMode = v;
			ThornsLocalSettings.Save();
		} );

		ThornsUiFactory.AddLabel( body, "KEYBINDS — click a row, then press a key (Esc cancels)", "thorns-header" );
		ThornsKeybindSettingsUi.Build( body );
	}

	static void AddStepper( Panel parent, string label, Func<float> get, Action<float> set, float step )
	{
		var row = ThornsUiFactory.AddPanel( parent, "stepper-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		ThornsUiFactory.AddLabel( row, label, "thorns-header" );
		var valueLabel = ThornsUiFactory.AddLabel( row, get().ToString( "0.00" ), "thorns-muted" );
		ThornsUiFactory.AddClickable( row, "thorns-btn-primary", "-", () =>
		{
			set( get() - step );
			valueLabel.Text = get().ToString( "0.00" );
		} );
		ThornsUiFactory.AddClickable( row, "thorns-btn-primary", "+", () =>
		{
			set( get() + step );
			valueLabel.Text = get().ToString( "0.00" );
		} );
	}

	static void AddToggleButton( Panel parent, string label, Func<bool> get, Action<bool> set )
	{
		var text = $"{label}: {(get() ? "ON" : "OFF")}";
		ThornsUiFactory.AddClickable( parent, "thorns-btn-primary", text, () => set( !get() ) );
	}

	static void AddSkinToggle( Panel parent )
	{
		var row = ThornsUiFactory.AddPanel( parent, "stepper-row ui-skin-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.MarginTop = Length.Pixels( 8 );
		ThornsUiFactory.AddLabel( row, "UI SKIN", "thorns-header" );
		var valueLabel = ThornsUiFactory.AddLabel( row, ThornsUiSkin.ActiveName, "thorns-muted ui-skin-value" );
		ThornsUiFactory.AddClickable( row, "thorns-btn-primary ui-skin-toggle", "Switch", () =>
		{
			ThornsUiSkin.CycleAndSave();
			valueLabel.Text = ThornsUiSkin.ActiveName;
		} );
		ThornsTheme.CreateMuted( parent, ThornsUiSkin.ActiveDescription ).AddClass( "ui-skin-hint" );
	}
}
