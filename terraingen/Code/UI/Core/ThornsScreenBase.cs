namespace Terraingen.UI.Core;

using Sandbox.UI;

/// <summary>Base class for menu tab content panels.</summary>
public abstract class ThornsScreenBase : Panel
{
	protected ThornsMenuHost Host { get; private set; }

	protected ThornsScreenBase( ThornsMenuHost host, Panel parent )
	{
		Host = host;
		Parent = parent;
		ThornsUiPanelDefaults.DisableDragScroll( this );
		AddClass( "thorns-screen" );
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Percent( 100 );
		Style.Display = DisplayMode.Flex;
		Style.FlexDirection = FlexDirection.Row;
		Style.MinHeight = Length.Pixels( 0 );
		Style.BackgroundColor = Color.Transparent;

		Build();
		UiRevisionBus.MenuRevisionChanged += HandleMenuRevision;
	}

	public bool IsMenuTabVisible =>
		Style.Display != DisplayMode.None && !HasClass( "thorns-tab-hidden" );

	public void SetTabVisible( bool visible )
	{
		Style.Display = visible ? DisplayMode.Flex : DisplayMode.None;
		SetClass( "thorns-tab-hidden", !visible );
	}

	void HandleMenuRevision( UiRevisionChannel channel, int revision )
	{
		if ( !IsMenuTabVisible )
			return;

		OnRevision( channel, revision );
	}

	public abstract void Rebuild();
	protected abstract void Build();

	protected virtual void OnRevision( UiRevisionChannel channel, int revision ) { }

	public virtual void OnShown( bool firstShow ) => Rebuild();

	public virtual void OnShown() => OnShown( firstShow: true );

	public void DisposeSubscriptions() =>
		UiRevisionBus.MenuRevisionChanged -= HandleMenuRevision;
}
