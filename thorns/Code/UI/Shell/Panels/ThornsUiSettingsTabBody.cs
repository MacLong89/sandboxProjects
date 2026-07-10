using System;
using Sandbox.UI;

namespace Sandbox;

public sealed class ThornsUiSettingsTabBody : Panel
{
	enum SettingsSection
	{
		Controls,
		Audio,
		Video
	}

	Panel _bodyHost;
	Panel _controlsScroll;
	Label _placeholder;
	ThornsUiCapsuleButton _btnControls;
	ThornsUiCapsuleButton _btnAudio;
	ThornsUiCapsuleButton _btnVideo;
	SettingsSection _section = SettingsSection.Controls;

	public ThornsUiSettingsTabBody()
	{
		AddClass( "thorns-tab-settings" );
		AddClass( "thorns-tab-settings-layout" );
		Style.FlexGrow = 1;
		Style.FlexShrink = 1;
		Style.MinHeight = 0;
		Style.Width = Length.Fraction( 1f );

		var nav = ThornsUiPanelAdd.AddChildPanel( this, "thorns-settings-nav" );
		nav.AddChild( new Label( "CATEGORIES", "thorns-tab-section-title" ) )
			.Style.PointerEvents = PointerEvents.None;

		_btnControls = nav.AddChild( new ThornsUiCapsuleButton( "Controls", "secondary",
			() => SetSection( SettingsSection.Controls ) ) );
		_btnAudio = nav.AddChild( new ThornsUiCapsuleButton( "Audio", "secondary",
			() => SetSection( SettingsSection.Audio ) ) );
		_btnVideo = nav.AddChild( new ThornsUiCapsuleButton( "Video", "secondary",
			() => SetSection( SettingsSection.Video ) ) );

		_bodyHost = ThornsUiPanelAdd.AddChildPanel( this, "thorns-settings-body" );
		_bodyHost.Style.FlexGrow = 1;
		_bodyHost.Style.FlexShrink = 1;
		_bodyHost.Style.MinWidth = 0;
		_bodyHost.Style.MinHeight = 0;

		_controlsScroll = ThornsUiPanelAdd.AddChildPanel( _bodyHost, "thorns-settings-controls-scroll" );
		_controlsScroll.Style.FlexDirection = FlexDirection.Column;
		_controlsScroll.Style.FlexGrow = 1;
		_controlsScroll.Style.MinHeight = 0;
		_controlsScroll.Style.Overflow = OverflowMode.Scroll;
		_controlsScroll.CanDragScroll = false;

		_placeholder = _bodyHost.AddChild( new Label( "", "thorns-settings-placeholder" ) );
		_placeholder.Style.PointerEvents = PointerEvents.None;
		_placeholder.Style.Display = DisplayMode.None;

		ThornsUiControlsReference.Populate( _controlsScroll );
		SetSection( SettingsSection.Controls );
	}

	void SetSection( SettingsSection section )
	{
		_section = section;
		_btnControls.SetClass( "active", section == SettingsSection.Controls );
		_btnAudio.SetClass( "active", section == SettingsSection.Audio );
		_btnVideo.SetClass( "active", section == SettingsSection.Video );

		var showControls = section == SettingsSection.Controls;
		_controlsScroll.Style.Display = showControls ? DisplayMode.Flex : DisplayMode.None;

		if ( showControls )
		{
			_placeholder.Style.Display = DisplayMode.None;
			return;
		}

		_placeholder.Style.Display = DisplayMode.Flex;
		_placeholder.Text = section switch
		{
			SettingsSection.Audio => "Audio settings are not available yet.",
			SettingsSection.Video => "Video settings are not available yet.",
			_ => ""
		};
	}
}
