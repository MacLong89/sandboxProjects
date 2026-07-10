namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.Animals;
using Terraingen.GameData;
using Terraingen.UI;

public enum ThornsScenePreviewMode
{
	Default,
	TameHero
}

// Large center-column preview — creature portrait PNG or optional .vmdl viewport.
public sealed class ThornsScenePreviewPanel : Panel
{
	readonly ThornsScenePreviewMode _mode;
	Panel _portrait;
	ThornsModelPreviewPanel _modelPreview;
	Label _caption;
	string _lastKey = "";

	public ThornsScenePreviewPanel( Panel parent, ThornsScenePreviewMode mode = ThornsScenePreviewMode.Default )
	{
		_mode = mode;
		Parent = parent;
		AddClass( "thorns-scene-preview" );
		if ( mode == ThornsScenePreviewMode.TameHero )
			AddClass( "thorns-scene-preview-tame-hero" );
		Style.FlexGrow = 1;
		Style.MinHeight = Length.Pixels( 0 );
		Style.Display = DisplayMode.Flex;
		Style.FlexDirection = FlexDirection.Column;
		Style.AlignItems = Align.Center;
		Style.JustifyContent = Justify.Center;
		Style.Overflow = OverflowMode.Hidden;

		_portrait = ThornsUiFactory.AddPanel( this, "tame-center-portrait slot-icon" );
		_portrait.Style.FlexGrow = 1;
		_portrait.Style.Width = Length.Percent( 100 );
		_portrait.Style.MinHeight = Length.Pixels( 0 );
		_portrait.Style.MaxWidth = Length.Percent( 78 );
		_portrait.Style.Display = DisplayMode.None;
		_portrait.Style.PointerEvents = PointerEvents.None;

		_caption = ThornsUiFactory.AddLabel( this, "", "preview-caption" );
		_caption.Style.FontSize = Length.Pixels( 13 );
		_caption.Style.LetterSpacing = Length.Pixels( 1 );
		_caption.Style.FontColor = ThornsTheme.TextSecondary;
		_caption.Style.MarginTop = Length.Pixels( 8 );
		if ( mode == ThornsScenePreviewMode.TameHero )
			_caption.Style.Display = DisplayMode.None;
	}

	ThornsModelPreviewPanel EnsureModelPreview()
	{
		if ( _modelPreview is not null && _modelPreview.IsValid )
			return _modelPreview;

		_modelPreview = new ThornsModelPreviewPanel( this, "thorns-scene-preview-viewport" );
		_modelPreview.Style.FlexGrow = 1;
		_modelPreview.Style.Width = Length.Percent( 100 );
		_modelPreview.Style.MinHeight = Length.Pixels( _mode == ThornsScenePreviewMode.TameHero ? 220 : 0 );
		_modelPreview.Style.MaxWidth = Length.Percent( _mode == ThornsScenePreviewMode.TameHero ? 100 : 78 );
		_modelPreview.Style.Display = DisplayMode.None;
		return _modelPreview;
	}

	/// <summary>Spinning species <c>.vmdl</c> for the tames hero column (falls back to portrait PNG).</summary>
	public void SetTamePreview( string modelPath, string speciesKey, string displayName = null, string animPrefix = null )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) && string.IsNullOrWhiteSpace( speciesKey ) )
		{
			ShowEmpty( "No preview" );
			return;
		}

		var key = $"tame|{modelPath}|{speciesKey}|{displayName}|{animPrefix}";
		if ( key == _lastKey )
			return;

		_lastKey = key;

		var resolvedModel = modelPath?.Trim() ?? "";
		var resolvedAnimPrefix = animPrefix?.Trim() ?? "";
		if ( ThornsAnimalSpeciesRegistry.TryGet( speciesKey, out var species ) )
		{
			if ( string.IsNullOrWhiteSpace( resolvedModel ) )
				resolvedModel = species.ModelPath ?? "";

			if ( string.IsNullOrWhiteSpace( resolvedAnimPrefix ) )
				resolvedAnimPrefix = species.AnimPrefix ?? "";
		}

		if ( string.IsNullOrWhiteSpace( resolvedModel ) )
		{
			SetPortrait( ThornsTameCatalog.CreaturePortraitPath( speciesKey ), displayName );
			return;
		}

		var idleSequence = string.IsNullOrWhiteSpace( resolvedAnimPrefix ) ? "" : $"{resolvedAnimPrefix}_idle";
		var presentation = ThornsModelPreviewPresentation.TameHero with { IdleSequence = idleSequence };
		if ( !TryShowModel( resolvedModel, displayName ?? "", presentation ) )
			SetPortrait( ThornsTameCatalog.CreaturePortraitPath( speciesKey ), displayName );
	}

	/// <summary>Compact weapon icon box in the inventory weapon inspector (left column).</summary>
	public void ConfigureWeaponInspectPreview()
	{
		AddClass( "inspect-weapon-preview" );
		Style.FlexGrow = 0;
		Style.FlexShrink = 0;
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.MinHeight = Length.Pixels( 0 );
		Style.MaxHeight = Length.Percent( 100 );
		Style.Overflow = OverflowMode.Hidden;
		Style.PointerEvents = PointerEvents.None;

		_portrait.Style.FlexGrow = 0;
		_portrait.Style.FlexShrink = 0;
		_portrait.Style.Width = Length.Percent( 100 );
		_portrait.Style.Height = Length.Percent( 100 );
		_portrait.Style.MinHeight = Length.Pixels( 0 );
		_portrait.Style.MaxHeight = Length.Percent( 100 );
		_portrait.Style.MaxWidth = Length.Percent( 100 );
		_portrait.AddClass( "inspect-weapon-preview-portrait" );

		if ( _modelPreview is not null && _modelPreview.IsValid )
		{
			_modelPreview.Style.FlexGrow = 0;
			_modelPreview.Style.FlexShrink = 0;
			_modelPreview.Style.Width = Length.Percent( 100 );
			_modelPreview.Style.Height = Length.Percent( 100 );
			_modelPreview.Style.MinHeight = Length.Pixels( 0 );
			_modelPreview.Style.MaxHeight = Length.Percent( 100 );
		}

		_caption.Style.Display = DisplayMode.None;
	}

	/// <summary>Compact portrait block for the inventory left column.</summary>
	public void ConfigureInventoryExplorerLayout()
	{
		AddClass( "inventory-explorer-preview" );
		Style.FlexGrow = 0;
		Style.FlexShrink = 0;
		Style.MaxHeight = Length.Pixels( Terraingen.UI.Core.ThornsUiMetrics.MenuExplorerPreviewMaxHeight );

		_portrait.Style.FlexGrow = 0;
		_portrait.Style.FlexShrink = 0;
		_portrait.Style.Height = Length.Pixels( Terraingen.UI.Core.ThornsUiMetrics.MenuExplorerPortraitHeight );
		_portrait.Style.MinHeight = Length.Pixels( Terraingen.UI.Core.ThornsUiMetrics.MenuExplorerPortraitHeight );
		_portrait.Style.MaxHeight = Length.Pixels( Terraingen.UI.Core.ThornsUiMetrics.MenuExplorerPortraitHeight );

		_caption.Style.FontSize = Length.Pixels( 9 );
		_caption.Style.MarginTop = Length.Pixels( 4 );
		_caption.Style.LineHeight = Length.Pixels( 12 );
	}

	/// <summary>Large hero preview between armor slots and attribute column.</summary>
	public void ConfigureInventoryArmorHeroLayout()
	{
		AddClass( "inventory-armor-hero-preview" );
		Style.FlexGrow = 1;
		Style.FlexShrink = 1;
		Style.Width = Length.Percent( 100 );
		Style.MinHeight = Length.Pixels( Terraingen.UI.Core.ThornsUiMetrics.MenuArmorHeroMinHeight );
		Style.MaxHeight = Length.Percent( 100 );

		_portrait.Style.FlexGrow = 1;
		_portrait.Style.FlexShrink = 1;
		_portrait.Style.Width = Length.Percent( 100 );
		_portrait.Style.MinHeight = Length.Pixels( 140 );
		_portrait.Style.MaxHeight = Length.Percent( 100 );
		_portrait.Style.MaxWidth = Length.Percent( 100 );

		_caption.Style.FontSize = Length.Pixels( 10 );
		_caption.Style.MarginTop = Length.Pixels( 6 );
		_caption.Style.LineHeight = Length.Pixels( 13 );
	}

	public void SetPortrait( string portraitPath, string caption = null )
	{
		var key = $"portrait|{portraitPath}|{caption}";
		if ( key == _lastKey )
			return;

		_lastKey = key;
		_caption.Text = (caption ?? "").ToUpper();

		if ( string.IsNullOrWhiteSpace( portraitPath ) )
		{
			ShowEmpty( "No preview" );
			return;
		}

		ShowPortraitMode();
		ThornsIconCache.ApplyToPanel( _portrait, portraitPath );
	}

	public void SetGameObject( GameObject target )
	{
		if ( !target.IsValid() )
		{
			ShowEmpty( "No preview" );
			return;
		}

		var brain = target.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndDescendants );
		if ( brain.IsValid() && ThornsAnimalSpeciesRegistry.TryGet( brain.SpeciesId, out var species ) )
		{
			SetPortrait( ThornsTameCatalog.CreaturePortraitPath( species.Key ), species.DisplayName );
			return;
		}

		ShowEmpty( target.Name );
	}

	public void SetSpeciesModel( string modelPath, string portraitPath = null, string caption = null )
	{
		if ( !string.IsNullOrWhiteSpace( portraitPath ) )
		{
			SetPortrait( portraitPath, caption );
			return;
		}

		TryShowModel( modelPath, caption ?? "", ThornsModelPreviewPresentation.Default );
	}

	bool TryShowModel( string modelPath, string caption, ThornsModelPreviewPresentation presentation )
	{
		var key = $"model|{modelPath}|{caption}|{presentation.AutoSpin}|{presentation.IdleSequence}";
		if ( key == _lastKey )
			return !string.IsNullOrWhiteSpace( modelPath );

		_lastKey = key;

		if ( _mode != ThornsScenePreviewMode.TameHero )
			_caption.Text = (caption ?? "").ToUpper();

		if ( string.IsNullOrWhiteSpace( modelPath ) )
		{
			ShowEmpty( "No preview" );
			return false;
		}

		ShowModelMode();
		return EnsureModelPreview().SetModel( modelPath, presentation );
	}

	void ShowPortraitMode()
	{
		_portrait.Style.Display = DisplayMode.Flex;
		if ( _modelPreview is not null && _modelPreview.IsValid )
		{
			_modelPreview.Style.Display = DisplayMode.None;
			_modelPreview.Clear();
		}
	}

	void ShowModelMode()
	{
		_portrait.Style.Display = DisplayMode.None;
		_portrait.Style.BackgroundImage = null;
		var preview = EnsureModelPreview();
		preview.Style.Display = DisplayMode.Flex;
	}

	void ShowEmpty( string message )
	{
		_lastKey = "";
		_portrait.Style.Display = DisplayMode.None;
		_portrait.Style.BackgroundImage = null;
		if ( _modelPreview is not null && _modelPreview.IsValid )
		{
			_modelPreview.Style.Display = DisplayMode.None;
			_modelPreview.Clear();
		}
		_caption.Text = message;
	}
}
