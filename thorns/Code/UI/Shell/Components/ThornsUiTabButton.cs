using System;
using Sandbox.UI;

namespace Sandbox;

public sealed class ThornsUiTabButton : Panel
{
	readonly Action _onSelect;

	public ThornsMainUiTab Tab { get; }

	public ThornsUiTabButton( ThornsMainUiTab tab, string label, Action onSelect )
	{
		Tab = tab;
		_onSelect = onSelect;
		AddClass( "thorns-tab-btn" );
		AddClass( $"thorns-tab-accent-{TabKey( tab )}" );
		Style.PointerEvents = PointerEvents.All;

		var cap = AddChild( new Label( label, "thorns-tab-btn-label" ) );
		cap.Style.PointerEvents = PointerEvents.None;
	}

	public override bool WantsMouseInput() => _onSelect is not null;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( e.MouseButton != MouseButtons.Left )
			return;
		_onSelect?.Invoke();
	}

	static string TabKey( ThornsMainUiTab t ) =>
		t switch
		{
			ThornsMainUiTab.Inventory => "inv",
			ThornsMainUiTab.Skills => "skills",
			ThornsMainUiTab.Tames => "tames",
			ThornsMainUiTab.Guild => "guild",
			ThornsMainUiTab.Journal => "journal",
			ThornsMainUiTab.Settings => "settings",
			_ => "inv"
		};

	public void SetSelected( bool sel )
	{
		SetClass( "active", sel );
	}
}
