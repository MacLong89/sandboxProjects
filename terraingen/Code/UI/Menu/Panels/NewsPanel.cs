namespace Terraingen.UI.Menu.Panels;

using Sandbox.UI;
using Terraingen.UI;

public sealed class NewsPanel : Panel
{
	string _patchNotesUrl;
	string _devBlogUrl;

	public NewsPanel( Panel parent )
	{
		parent.AddChild( this );
		AddClass( "mainmenu-news" );
		ThornsTheme.ApplyGlassPanel( this );
		Style.FlexDirection = FlexDirection.Column;
		Style.Padding = Length.Pixels( 14 );
		Style.MaxWidth = Length.Pixels( 340 );

		UiRevisionBus.MenuRevisionChanged += OnRevision;
		Rebuild();
	}

	public override void OnDeleted()
	{
		UiRevisionBus.MenuRevisionChanged -= OnRevision;
		base.OnDeleted();
	}

	void OnRevision( UiRevisionChannel channel, int _ )
	{
		if ( channel == UiRevisionChannel.Menu )
			Rebuild();
	}

	void Rebuild()
	{
		DeleteChildren();
		_patchNotesUrl = null;
		_devBlogUrl = null;

		if ( !ThornsMenuNews.HasContent )
		{
			SetClass( "mainmenu-hidden", true );
			return;
		}

		ThornsUiFactory.AddLabel( this, "LATEST UPDATE", "mainmenu-news-kicker" );

		var entry = ThornsMenuNews.GetLatest();
		if ( entry is null )
			return;

		ThornsUiFactory.AddLabel( this, entry.Title, "mainmenu-news-title" );
		if ( !string.IsNullOrWhiteSpace( entry.Summary ) )
			ThornsUiFactory.AddLabel( this, entry.Summary, "mainmenu-news-summary" );

		var links = ThornsUiFactory.AddPanel( this, "mainmenu-news-links" );
		links.Style.FlexDirection = FlexDirection.Row;

		if ( !string.IsNullOrWhiteSpace( entry.PatchNotesUrl ) )
		{
			_patchNotesUrl = entry.PatchNotesUrl;
			ThornsUiFactory.AddClickable( links, "mainmenu-news-link", "Patch Notes", OnPatchNotesClicked );
		}

		if ( !string.IsNullOrWhiteSpace( entry.DevBlogUrl ) )
		{
			_devBlogUrl = entry.DevBlogUrl;
			ThornsUiFactory.AddClickable( links, "mainmenu-news-link", "Dev Blog", OnDevBlogClicked );
		}
	}

	void OnPatchNotesClicked()
	{
		if ( !string.IsNullOrWhiteSpace( _patchNotesUrl ) )
			Log.Info( $"[Thorns Menu] Patch notes: {_patchNotesUrl}" );
	}

	void OnDevBlogClicked()
	{
		if ( !string.IsNullOrWhiteSpace( _devBlogUrl ) )
			Log.Info( $"[Thorns Menu] Dev blog: {_devBlogUrl}" );
	}
}
