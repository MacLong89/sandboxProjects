using System.Collections.Generic;

using Sandbox.UI;



namespace Sandbox;



/// <summary>Top-right kill feed — killer / victim lines with role-colored names.</summary>

public sealed class YaKillFeedPanel : Panel

{

	const int MaxVisible = 6;

	const float EntryTtlSeconds = 8f;

	const float FadeSeconds = 1.35f;



	readonly List<FeedEntry> _entries = new();

	YaPlayerRole _localRole = YaPlayerRole.Unassigned;



	sealed class FeedEntry

	{

		public Panel Row;

		public double ExpireAt;

	}



	public YaKillFeedPanel()

	{

		AddClass( "ya-hud-kill-feed" );

		Style.FlexDirection = FlexDirection.Column;

		Style.AlignItems = Align.FlexEnd;

	}



	public void SetLocalRole( YaPlayerRole role ) => _localRole = role;



	public void PushElimination( string killer, YaPlayerRole killerRole, string victim, YaPlayerRole victimRole )

	{

		if ( string.IsNullOrWhiteSpace( killer ) || string.IsNullOrWhiteSpace( victim ) )

			return;



		TrimOldestIfNeeded();

		var row = AddChild<Panel>( "ya-hud-kill-feed__row" );

		row.Style.FlexDirection = FlexDirection.Row;

		row.Style.AlignItems = Align.Center;

		row.Style.JustifyContent = Justify.FlexEnd;

		row.Style.MarginBottom = 3;

		row.Style.FlexShrink = 0;



		AddNameLabel( row, killer, killerRole, highlight: IsLocalName( killer ) );

		AddMutedLabel( row, " eliminated " );

		AddNameLabel( row, victim, victimRole, highlight: IsLocalName( victim ) );



		_entries.Add( new FeedEntry { Row = row, ExpireAt = Time.Now + EntryTtlSeconds } );

	}



	public void PushInfo( string message )

	{

		if ( string.IsNullOrWhiteSpace( message ) )

			return;



		TrimOldestIfNeeded();

		var row = AddChild<Panel>( "ya-hud-kill-feed__row ya-hud-kill-feed__row--info" );

		row.Style.FlexDirection = FlexDirection.Row;

		row.Style.AlignItems = Align.Center;

		row.Style.JustifyContent = Justify.FlexEnd;

		row.Style.MarginBottom = 3;

		row.Style.FlexShrink = 0;



		var lbl = row.AddChild( new Label( message.Trim(), "ya-hud-kill-feed__info" ) );

		lbl.Style.FontSize = 12;

		lbl.Style.FontWeight = 700;

		lbl.Style.FontColor = YaHudRoleTheme.For( _localRole ).Accent;

		lbl.Style.TextAlign = TextAlign.Right;



		_entries.Add( new FeedEntry { Row = row, ExpireAt = Time.Now + EntryTtlSeconds } );

	}



	public void TickFade()

	{

		var now = Time.Now;

		for ( var i = _entries.Count - 1; i >= 0; i-- )

		{

			var e = _entries[i];

			if ( e.Row is null || !e.Row.IsValid() )

			{

				_entries.RemoveAt( i );

				continue;

			}



			var fadeStart = e.ExpireAt - FadeSeconds;

			if ( now < fadeStart )

			{

				e.Row.Style.Opacity = 1f;

				continue;

			}



			if ( now >= e.ExpireAt )

			{

				e.Row.Delete();

				_entries.RemoveAt( i );

				continue;

			}



			var u = (float)((now - fadeStart) / (e.ExpireAt - fadeStart));

			e.Row.Style.Opacity = Math.Clamp( 1f - u, 0f, 1f );

		}

	}



	void TrimOldestIfNeeded()

	{

		while ( _entries.Count >= MaxVisible )

		{

			var old = _entries[0];

			_entries.RemoveAt( 0 );

			old.Row?.Delete();

		}

	}



	static void AddNameLabel( Panel row, string name, YaPlayerRole role, bool highlight )

	{

		var lbl = row.AddChild( new Label( name, "ya-hud-kill-feed__name" ) );

		lbl.Style.FontSize = 13;

		lbl.Style.FontWeight = highlight ? 900 : 700;

		lbl.Style.FontColor = YaHudRoleTheme.TeamNameColor( role );

		lbl.Style.TextAlign = TextAlign.Right;

		lbl.Style.FlexShrink = 0;

	}



	static void AddMutedLabel( Panel row, string text )

	{

		var lbl = row.AddChild( new Label( text, "ya-hud-kill-feed__sep" ) );

		lbl.Style.FontSize = 12;

		lbl.Style.FontWeight = 600;

		lbl.Style.FontColor = YaHudTheme.TextMuted;

		lbl.Style.TextAlign = TextAlign.Right;

		lbl.Style.FlexShrink = 0;

	}



	static bool IsLocalName( string name )

	{

		var local = Connection.Local?.DisplayName;

		return !string.IsNullOrWhiteSpace( local )

		       && string.Equals( local, name, StringComparison.OrdinalIgnoreCase );

	}

}


