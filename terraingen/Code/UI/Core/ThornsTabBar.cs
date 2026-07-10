namespace Terraingen.UI.Core;

using Sandbox.UI;
using Terraingen.UI;

/// <summary>Tab labels on a single shared wood rail — active state is text highlight only.</summary>
public sealed class ThornsTabBar
{
	readonly Panel _root;
	readonly List<TabButton> _tabs = new();
	readonly Action<string> _onSelected;

	public ThornsTabBar( Panel parent, Action<string> onSelected )
	{
		_onSelected = onSelected;
		_root = ThornsUiFactory.AddPanel( parent, "thorns-tab-bar thorns-tab-bar-rail" );
		_root.Style.FlexGrow = 1f;
		_root.Style.FlexShrink = 1f;
		_root.Style.MinWidth = Length.Pixels( 0 );
	}

	public void BuildTabs( IReadOnlyList<string> tabIds )
	{
		_root.DeleteChildren( true );
		_tabs.Clear();

		foreach ( var id in tabIds )
		{
			var button = new TabButton( _root, id, TabLabel( id ), _onSelected );
			_tabs.Add( button );
		}
	}

	public void SetActive( string tabId )
	{
		foreach ( var tab in _tabs )
			tab.SetClass( "active", string.Equals( tab.TabId, tabId, StringComparison.OrdinalIgnoreCase ) );
	}

	static string TabLabel( string tabId ) => tabId switch
	{
		"Inventory" => "INVENTORY",
		"Journal" => "JOURNAL",
		"Tames" => "TAMES",
		"Skills" => "SKILLS",
		"Map" => "MAP",
		"Guild" => "GUILD",
		"Settings" => "SETTINGS",
		_ => tabId.ToUpperInvariant()
	};

	sealed class TabButton : ThornsClickPanel
	{
		public string TabId { get; }
		readonly Action<string> _onSelected;

		public TabButton( Panel parent, string tabId, string label, Action<string> onSelected )
		{
			TabId = tabId;
			_onSelected = onSelected;
			parent.AddChild( this );
			AddClass( "thorns-tab thorns-tab-rail-item" );
			SetClick( OnTabClicked );
			ThornsUiFactory.AddPassiveLabel( this, label, "thorns-tab-label" );
		}

		void OnTabClicked() => _onSelected?.Invoke( TabId );
	}
}
