#nullable disable

using System;
using Sandbox.UI;

namespace Sandbox;

public sealed partial class ThornsGameShell : IThornsHudPresenter
{
	readonly ThornsHudCoordinator _hud = new();

	void BindHudServices() => _hud.BindPresenter( this );

	void IThornsHudPresenter.EnsureGameplayOverlayPanels()
	{
		if ( _hudMaskedLayer is null || !_hudMaskedLayer.IsValid )
			return;

		EnsureTameHudBuilt();
	}

	void EnsureGameplayOverlayPanels() => ((IThornsHudPresenter)this).EnsureGameplayOverlayPanels();

	ThornsToastBusEntry IThornsHudPresenter.OnToastEnqueued( ThornsToastBusEntry entry )
	{
		if ( _toastFeed is null || !_toastFeed.IsValid )
			return entry;

		var lbl = _toastFeed.AddChild( new Label( entry.Message, ThornsToastBus.CssClassForKind( entry.Kind ) ) );
		lbl.Style.PointerEvents = PointerEvents.None;
		lbl.Style.WhiteSpace = WhiteSpace.Normal;
		lbl.Style.TextAlign = TextAlign.Left;
		lbl.Style.MarginBottom = Length.Pixels( 4 );
		return new ThornsToastBusEntry( entry.Message, entry.Kind, entry.ExpireAt, lbl );
	}

	void IThornsHudPresenter.OnToastRemoved( ThornsToastBusEntry entry )
	{
		if ( entry.UserData is Panel p && p.IsValid )
			p.Delete();
	}

	void IThornsHudPresenter.OnInteractionHintChanged() => SyncInteractionHintToView();

	Component IThornsHudPresenter.GetComponentForHintProjection() => this;

	void SyncInteractionHintToView()
	{
		EnsureGameplayOverlayPanels();
		if ( _interactionHint is null || !_interactionHint.IsValid )
			return;

		var bus = _hud.Interaction;
		var t = bus.Message;
		_interactionHint.Text = t;
		_interactionHint.SetClass( "thorns-shell-interaction-hint--world", bus.HasWorldAnchor );
		if ( bus.HasWorldAnchor )
			UpdateInteractionHintWorldPosition();
		else
		{
			const float w = 360f;
			_interactionHint.Style.Left = Length.Fraction( 0.5f );
			_interactionHint.Style.Top = Length.Fraction( 0.56f );
			_interactionHint.Style.MarginLeft = Length.Pixels( w * -0.5f );
			_interactionHint.Style.MarginTop = Length.Pixels( -19f );
			_interactionHint.Style.Width = Length.Pixels( w );
		}

		_interactionHint.SetClass( "thorns-shell-interaction-hint--hidden", string.IsNullOrWhiteSpace( t ) );
	}

	void UpdateInteractionHintWorldPosition()
	{
		if ( _interactionHint is null || !_interactionHint.IsValid )
			return;

		var bus = _hud.Interaction;
		if ( !bus.HasWorldAnchor || string.IsNullOrWhiteSpace( bus.Message ) )
			return;

		if ( !bus.TryProjectAnchor( this, out var screenPos ) )
		{
			_interactionHint.SetClass( "thorns-shell-interaction-hint--hidden", true );
			return;
		}

		const float w = 320f;
		const float h = 38f;
		var left = Math.Clamp( screenPos.x - w * 0.5f, 12f, Math.Max( 12f, Screen.Width - w - 12f ) );
		var top = Math.Clamp( screenPos.y - h * 0.5f, 12f, Math.Max( 12f, Screen.Height - h - 12f ) );

		_interactionHint.Style.Left = Length.Pixels( left );
		_interactionHint.Style.Top = Length.Pixels( top );
		_interactionHint.Style.MarginLeft = Length.Pixels( 0f );
		_interactionHint.Style.MarginTop = Length.Pixels( 0f );
		_interactionHint.Style.Width = Length.Pixels( w );
		_interactionHint.SetClass( "thorns-shell-interaction-hint--hidden", false );
	}
}
