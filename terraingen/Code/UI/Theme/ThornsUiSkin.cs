namespace Terraingen.UI;

using Sandbox.UI;
using Terraingen.UI.Core;

/// <summary>
/// Client UI skins — three distinct visual systems:
/// Classic (fantasy chrome), Survive (modern atmospheric), Field (expedition dossier).
/// </summary>
public enum ThornsUiSkinKind
{
	Classic,
	Survive,
	Field
}

public static class ThornsUiSkin
{
	public const string ClassicClass = "ui-skin-classic";
	public const string SurviveClass = "ui-skin-survive";
	public const string FieldClass = "ui-skin-field";

	static readonly ThornsUiSkinKind[] CycleOrder =
	{
		ThornsUiSkinKind.Classic,
		ThornsUiSkinKind.Survive,
		ThornsUiSkinKind.Field
	};

	// Wildfall: a single canonical skin. The old three-skin system is collapsed —
	// Active always resolves to the Classic chrome pipeline, which we have fully
	// re-skinned to the weathered wood/iron/parchment/gold "Wildfall" look.
	public static ThornsUiSkinKind Active => ThornsUiSkinKind.Classic;

	public static string ActiveName => "Wildfall";

	public static string ActiveDescription => "Weathered frontier chrome — wood, iron, parchment, and gold.";

	public static void ApplyRoot( Panel root )
	{
		if ( root is null || !root.IsValid )
			return;

		root.SetClass( ClassicClass, Active == ThornsUiSkinKind.Classic );
		root.SetClass( SurviveClass, Active == ThornsUiSkinKind.Survive );
		root.SetClass( FieldClass, Active == ThornsUiSkinKind.Field );
	}

	public static void CycleAndSave()
	{
		var idx = Array.IndexOf( CycleOrder, Active );
		if ( idx < 0 )
			idx = 0;

		var next = CycleOrder[(idx + 1) % CycleOrder.Length];
		ThornsLocalSettings.Current.UiSkin = next.ToString();
		ThornsLocalSettings.Save();
		RequestRuntimeRebuild();
	}

	public static void RequestRuntimeRebuild()
	{
		ThornsMenuHost.Instance?.RequestSkinRebuild();
		Menu.MainMenuHost.Instance?.RequestSkinRebuild();
	}

	public static ThornsUiSkinKind Parse( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return ThornsUiSkinKind.Classic;

		if ( string.Equals( value, nameof( ThornsUiSkinKind.Survive ), StringComparison.OrdinalIgnoreCase )
		     || string.Equals( value, "Survive", StringComparison.OrdinalIgnoreCase ) )
			return ThornsUiSkinKind.Survive;

		if ( string.Equals( value, nameof( ThornsUiSkinKind.Field ), StringComparison.OrdinalIgnoreCase )
		     || string.Equals( value, "Field", StringComparison.OrdinalIgnoreCase ) )
			return ThornsUiSkinKind.Field;

		return ThornsUiSkinKind.Classic;
	}
}
