namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed class ThornsSettingsScreen : ThornsScreenBase
{
	public ThornsSettingsScreen( ThornsMenuHost host, Panel parent ) : base( host, parent ) { }

	protected override void Build()
	{
		Style.FlexDirection = FlexDirection.Row;

		var left = ThornsUiFactory.AddPanel( this, "thorns-menu-column thorns-col-center settings-panel-left" );
		ThornsTheme.ApplyMenuPanel( left );
		left.Style.Padding = Length.Pixels( 20 );
		left.Style.FlexGrow = 1f;
		left.Style.FlexDirection = FlexDirection.Column;
		left.Style.Overflow = OverflowMode.Scroll;
		ThornsTheme.CreateSectionHeader( left, "SETTINGS" );
		BuildSettingsControls( left );

		var right = ThornsUiFactory.AddPanel( this, "thorns-menu-column thorns-col-right settings-panel-right" );
		ThornsTheme.ApplyMenuPanel( right );
		right.Style.Padding = Length.Pixels( 20 );
		right.Style.FlexDirection = FlexDirection.Column;
		right.Style.Overflow = OverflowMode.Scroll;
		ThornsTheme.CreateSectionHeader( right, "GAME INFO" );
		ThornsTheme.CreateMuted( right, "Thorns — Bloom testing stage." );
		ThornsTheme.CreateHeader( right, "SUPPORT", "settings-support-heading" );
		ThornsUiFactory.AddClickable( right, "settings-support-btn", "UI SKIN", () =>
		{
			ThornsUiSkin.CycleAndSave();
			ThornsNotificationBus.Push( $"UI skin: {ThornsUiSkin.ActiveName}", "info", 3f );
		} );
	}

	static void BuildSettingsControls( Panel body )
	{
		AddStepper( body, "UI SCALE", () => ThornsLocalSettings.Current.UiScale, v =>
		{
			ThornsLocalSettings.Current.UiScale = Math.Clamp( v, 0.75f, 1.5f );
			ThornsLocalSettings.Save();
			ThornsMenuHost.Instance?.ApplyUiScale();
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

		ThornsKeybindSettingsUi.Build( body );
	}

	static void AddStepper( Panel parent, string label, Func<float> get, Action<float> set, float step )
	{
		var row = ThornsUiFactory.AddPanel( parent, "stepper-row settings-stepper-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		ThornsUiFactory.AddLabel( row, label, "settings-row-label" );
		var valueLabel = ThornsUiFactory.AddLabel( row, get().ToString( "0.00" ), "settings-row-value" );
		ThornsUiFactory.AddClickable( row, "settings-stepper-btn", "-", () =>
		{
			set( get() - step );
			valueLabel.Text = get().ToString( "0.00" );
		} );
		ThornsUiFactory.AddClickable( row, "settings-stepper-btn", "+", () =>
		{
			set( get() + step );
			valueLabel.Text = get().ToString( "0.00" );
		} );
	}

	static void AddToggleButton( Panel parent, string label, Func<bool> get, Action<bool> set )
	{
		var text = $"{label}: {(get() ? "ON" : "OFF")}";
		ThornsUiFactory.AddClickable( parent, "settings-toggle-btn", text, () =>
		{
			set( !get() );
		} );
	}

	static void AddSkinToggle( Panel parent )
	{
		var row = ThornsUiFactory.AddPanel( parent, "ui-skin-row settings-stepper-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		ThornsUiFactory.AddLabel( row, "UI SKIN", "settings-row-label" );
		var valueLabel = ThornsUiFactory.AddLabel( row, ThornsUiSkin.ActiveName, "settings-row-value" );
		ThornsUiFactory.AddClickable( row, "settings-stepper-btn", "CYCLE", () =>
		{
			ThornsUiSkin.CycleAndSave();
			valueLabel.Text = ThornsUiSkin.ActiveName;
		} );
		ThornsTheme.CreateMuted( parent, ThornsUiSkin.ActiveDescription ).AddClass( "ui-skin-hint" );
	}

	public override void Rebuild() { }
}
