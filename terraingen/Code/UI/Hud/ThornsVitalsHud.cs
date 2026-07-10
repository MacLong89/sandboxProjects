namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed class ThornsVitalsHud
{
	readonly Panel _root;
	readonly Panel _levelWrap;
	readonly Label _level;
	readonly ThornsHudBar _health;
	readonly ThornsHudBar _stamina;
	readonly ThornsHudBar _thirst;
	readonly ThornsHudBar _hunger;
	readonly Panel _warmthBadge;
	const float LowVitalFraction = 0.35f;

	public ThornsVitalsHud( Panel parent )
	{
		var classic = ThornsHudClassicChrome.IsActive;

		_root = ThornsUiFactory.AddPanel( parent, classic ? "vitals-hud vitals-hud-classic" : "vitals-hud" );
		_root.Style.Position = PositionMode.Absolute;
		if ( classic )
		{
			ThornsHudTheme.ApplyClassicVitalsPanel( _root );
			_root.Style.Left = Length.Pixels( ThornsHudTheme.ClassicVitalsLeftPx );
			_root.Style.Bottom = Length.Pixels( ThornsHudTheme.ClassicVitalsBottomPx );
			_root.Style.Top = Length.Auto;
			_root.Style.Width = Length.Pixels( ThornsHudTheme.ClassicVitalsPanelWidthPx );
			_root.Style.MaxWidth = Length.Pixels( ThornsHudTheme.ClassicVitalsPanelWidthPx );
			_root.Style.Overflow = OverflowMode.Hidden;
			_root.Style.FlexDirection = FlexDirection.Column;
		}
		else
		{
			_root.Style.Left = Length.Pixels( ThornsHudTheme.VitalsClusterLeftPx );
			_root.Style.Top = Length.Pixels( ThornsHudTheme.VitalsClusterTopPx );
			_root.Style.FlexDirection = FlexDirection.Row;
		}

		_root.Style.AlignItems = Align.Stretch;

		var stackHeight = ThornsHudTheme.VitalsBarStackHeightPx;
		var diamondSide = ThornsHudTheme.VitalsLevelDiamondSidePx;

		_levelWrap = ThornsUiFactory.AddPanel( _root, "hud-level-wrap" );
		if ( !classic )
		{
			_levelWrap.Style.Height = Length.Pixels( stackHeight );
			_levelWrap.Style.MinHeight = Length.Pixels( stackHeight );
			_levelWrap.Style.Width = Length.Pixels( diamondSide + ThornsHudTheme.VitalsLevelWrapExtraWidthPx );
			_levelWrap.Style.MinWidth = Length.Pixels( diamondSide + ThornsHudTheme.VitalsLevelWrapExtraWidthPx );
			_levelWrap.Style.JustifyContent = Justify.Center;
			_levelWrap.Style.AlignItems = Align.Center;

			var diamond = ThornsUiFactory.AddPanel( _levelWrap, "hud-level-diamond" );
			diamond.Style.Width = Length.Pixels( diamondSide );
			diamond.Style.Height = Length.Pixels( diamondSide );
			diamond.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0.28f );
			diamond.Style.BorderColor = new Color( 1f, 1f, 1f, 0.12f );
			diamond.Style.BorderWidth = Length.Pixels( 1 );
			var diamondInner = ThornsUiFactory.AddPanel( diamond, "hud-level-diamond-inner" );
			_level = ThornsUiFactory.AddLabel( diamondInner, "1", "hud-level-number" );
			_level.Style.FontSize = Length.Pixels( ThornsHudTheme.VitalsLevelNumberFontPx );
		}
		else
		{
			_levelWrap.Style.Display = DisplayMode.None;
			_level = ThornsUiFactory.AddLabel( _levelWrap, "", "hud-level-number" );
			_level.Style.Display = DisplayMode.None;
		}

		var bars = ThornsUiFactory.AddPanel( _root, "hud-vitals-bars" );
		bars.Style.FlexDirection = FlexDirection.Column;
		if ( classic )
		{
			bars.Style.Width = Length.Percent( 100 );
			bars.Style.MaxWidth = Length.Percent( 100 );
			bars.Style.MinWidth = Length.Pixels( 0 );
		}
		else
		{
			bars.Style.Height = Length.Pixels( stackHeight );
			bars.Style.MinHeight = Length.Pixels( stackHeight );
			bars.Style.MarginLeft = Length.Pixels( ThornsHudTheme.VitalsBarsMarginLeftPx );
		}

		_health = new ThornsHudBar( bars, "health", ThornsHudTheme.HealthFill, ThornsHudBarTier.Medium,
			fillParentWidth: classic, iconPath: ThornsIconRegistry.Hud( "health" ) );
		_stamina = new ThornsHudBar( bars, "stamina", ThornsHudTheme.StaminaFill, ThornsHudBarTier.Medium,
			fillParentWidth: classic, iconPath: ThornsIconRegistry.Hud( "stamina" ) );
		_thirst = new ThornsHudBar( bars, "thirst", ThornsHudTheme.ThirstFill, ThornsHudBarTier.Medium,
			fillParentWidth: classic, iconPath: ThornsIconRegistry.Hud( "water" ) );
		_hunger = new ThornsHudBar( bars, "hunger", ThornsHudTheme.HungerFill, ThornsHudBarTier.Medium,
			fillParentWidth: classic, iconPath: ThornsIconRegistry.Hud( "food" ) );

		_warmthBadge = ThornsUiFactory.AddPanel( bars, "vitals-warmth-badge" );
		_warmthBadge.Style.Display = DisplayMode.None;
		ThornsUiFactory.AddLabel( _warmthBadge, "WARM", "vitals-warmth-label" );
	}

	public void Refresh()
	{
		if ( !_root.IsValid )
			return;

		if ( !ThornsUiClientState.HasSnapshot )
			return;

		var skills = ThornsUiClientState.Snapshot.Skills ?? new();
		var v = ThornsUiClientState.Snapshot.Vitals ?? new();
		var level = Math.Max( 1, skills.PlayerLevel );

		if ( _level.IsValid && _level.Style.Display != DisplayMode.None )
			_level.Text = ThornsUiSkin.Active == ThornsUiSkinKind.Field ? $"LV {level}" : $"{level}";

		_health.Set( v.Health, Math.Max( 1f, v.MaxHealth ) );

		_stamina.SetVisible( true );
		if ( v.ShowStamina )
			_stamina.Set( v.Stamina, Math.Max( 1f, v.MaxStamina ) );

		_thirst.Set( v.Water, Math.Max( 1f, v.MaxWater ) );
		_hunger.Set( v.Food, Math.Max( 1f, v.MaxFood ) );

		_thirst.SetLowWarning( v.MaxWater > 0f && v.Water / v.MaxWater <= LowVitalFraction );
		_hunger.SetLowWarning( v.MaxFood > 0f && v.Food / v.MaxFood <= LowVitalFraction );

		if ( _warmthBadge.IsValid )
			_warmthBadge.Style.Display = v.HasCampfireWarmth ? DisplayMode.Flex : DisplayMode.None;
	}

	public void TickLowWarningPulse()
	{
		_thirst.TickLowWarningPulse();
		_hunger.TickLowWarningPulse();
	}
}
