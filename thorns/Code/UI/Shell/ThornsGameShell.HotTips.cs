#nullable disable

using System;
using System.Collections.Generic;
using Sandbox.UI;

namespace Sandbox;

public sealed partial class ThornsGameShell
{
	Panel _hotTipFeed;
	readonly Queue<ThornsHotTipDefinition> _hotTipQueue = new();
	readonly List<HotTipSlot> _hotTipActive = new();

	sealed class HotTipSlot
	{
		public Panel Panel;
		public Label Label;
		public double FadeOutAt;
		public double RemoveAt;
		public bool Fading;
	}

	public int HotTipSlotCount => _hotTipActive.Count;

	public bool EnqueueHotTip( ThornsHotTipDefinition def )
	{
		if ( !IsLocalOwned || string.IsNullOrWhiteSpace( def.Message ) )
			return false;

		if ( !IsLocalHudReady )
			return false;

		EnsureHotTipFeedBuilt();

		if ( _hotTipActive.Count >= 2 )
		{
			if ( _hotTipQueue.Count >= 4 )
				return false;

			_hotTipQueue.Enqueue( def );
			return true;
		}

		ShowHotTipNow( def );
		return true;
	}

	/// <summary>Fade/queue presentation — called from <see cref="ThornsHotTipDirector"/> only (not shell <c>OnUpdate</c>).</summary>
	public void TickHotTips()
	{
		if ( !IsLocalOwned || !IsLocalHudReady )
			return;

		var now = Time.Now;

		for ( var i = _hotTipActive.Count - 1; i >= 0; i-- )
		{
			var slot = _hotTipActive[i];
			if ( slot.Panel is null || !slot.Panel.IsValid )
			{
				_hotTipActive.RemoveAt( i );
				continue;
			}

			if ( !slot.Fading && now >= slot.FadeOutAt )
			{
				slot.Fading = true;
				slot.Panel.AddClass( "thorns-hot-tip--fading" );
			}

			if ( now < slot.RemoveAt )
				continue;

			slot.Panel.Delete();
			_hotTipActive.RemoveAt( i );
		}

		while ( _hotTipActive.Count < 2 && _hotTipQueue.Count > 0 )
			ShowHotTipNow( _hotTipQueue.Dequeue() );
	}

	void ShowHotTipNow( ThornsHotTipDefinition def )
	{
		EnsureHotTipFeedBuilt();
		if ( _hotTipFeed is null || !_hotTipFeed.IsValid )
			return;

		var cat = def.Category.ToString().ToLowerInvariant();
		var lbl = _hotTipFeed.AddChild(
			new Label( ThornsInteractionPromptText.Format( def.Message.Trim() ), $"thorns-hot-tip thorns-hot-tip--{cat}" ) );
		lbl.Style.PointerEvents = PointerEvents.None;
		lbl.Style.WhiteSpace = WhiteSpace.Normal;
		lbl.Style.TextAlign = TextAlign.Left;
		lbl.Style.MarginBottom = Length.Pixels( 6 );
		lbl.Style.Opacity = 1f;

		var dur = Math.Max( 2.5f, def.DurationSeconds );
		_hotTipActive.Add( new HotTipSlot
		{
			Panel = lbl,
			Label = lbl,
			FadeOutAt = Time.Now + dur - 0.45f,
			RemoveAt = Time.Now + dur,
			Fading = false
		} );
	}

	void EnsureHotTipFeedBuilt()
	{
		if ( !IsLocalHudReady )
			return;

		EnsureTameHudBuilt();
		if ( _tameHudColumn is null || !_tameHudColumn.IsValid )
			return;

		if ( _hotTipFeed is { IsValid: true } )
			return;

		_hotTipFeed = ThornsUiPanelAdd.AddChildPanel( _tameHudColumn, "thorns-hot-tip-feed" );
		_hotTipFeed.Style.Display = DisplayMode.Flex;
		_hotTipFeed.Style.FlexDirection = FlexDirection.Column;
		_hotTipFeed.Style.JustifyContent = Justify.FlexStart;
		_hotTipFeed.Style.AlignItems = Align.FlexStart;
		_hotTipFeed.Style.FlexShrink = 0;
		_hotTipFeed.Style.PointerEvents = PointerEvents.None;
		_hotTipFeed.Style.Width = Length.Percent( 100f );
		_hotTipFeed.Style.MaxWidth = Length.Pixels( 420 );
	}

	void ClearHotTips()
	{
		_hotTipQueue.Clear();
		foreach ( var slot in _hotTipActive )
		{
			if ( slot.Panel is { IsValid: true } )
				slot.Panel.Delete();
		}

		_hotTipActive.Clear();
	}
}
