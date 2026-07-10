#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>Short-lived world-space "-{damage}" label (scene-parented so it survives immediate NPC despawn).</summary>
public static class ThornsDamageFloaterWorld
{
	const float WorldPanelScale = 0.38f;

	public static void Spawn( GameObject damagedRoot, float damageAmount, float jitterX, float jitterY )
	{
		if ( !Game.IsPlaying || damagedRoot is null || !damagedRoot.IsValid() || damageAmount <= 0.001f )
			return;

		var scene = damagedRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var cc = damagedRoot.Components.GetInAncestorsOrSelf<CharacterController>( true );
		var anchorZ = cc.IsValid() ? cc.Height * 0.55f : 44f;

		var go = new GameObject( true, "DamageFloater" );
		go.Parent = scene;
		go.WorldPosition = damagedRoot.WorldPosition + Vector3.Up * anchorZ + new Vector3( jitterX, jitterY, 0f );
		go.LocalScale = WorldPanelScale;

		var wp = go.Components.Create<WorldPanel>();
		wp.InteractionRange = 0f;

		var panel = go.Components.Create<ThornsDamageFloaterLabelPanel>();
		panel.SetText( $"-{MathF.Round( damageAmount ):0}" );

		go.Components.Create<ThornsDamageFloaterAnimator>();
	}
}

/// <summary>Minimal PanelComponent rendered via parent <see cref="WorldPanel"/>.</summary>
public sealed class ThornsDamageFloaterLabelPanel : PanelComponent
{
	Label _label;
	bool _treeReady;

	internal void EnsureUiTree()
	{
		if ( _treeReady )
			return;

		if ( Panel is null || !Panel.IsValid )
			return;

		Panel.Style.PointerEvents = PointerEvents.None;
		Panel.Style.BackgroundColor = Color.Transparent;
		Panel.Style.PaddingLeft = Length.Pixels( 6 );
		Panel.Style.PaddingRight = Length.Pixels( 6 );
		Panel.Style.PaddingTop = Length.Pixels( 2 );
		Panel.Style.PaddingBottom = Length.Pixels( 2 );
		Panel.Style.JustifyContent = Justify.Center;
		Panel.Style.AlignItems = Align.Center;

		Panel.AddClass( "thorns-damage-floater-root" );

		_label = Panel.AddChild( new Label() );
		_label.Style.FontSize = 28;
		_label.Style.FontWeight = 900;
		_label.Style.TextAlign = TextAlign.Center;
		_label.Style.FontColor = new Color( 1f, 0.38f, 0.28f, 1f );

		_treeReady = true;
	}

	public void SetText( string text )
	{
		EnsureUiTree();
		if ( _label is null || !_label.IsValid )
			return;

		_label.Text = text ?? "";
	}

	public void SetOpacity( float opacity )
	{
		EnsureUiTree();
		if ( Panel is null || !Panel.IsValid )
			return;

		Panel.Style.Opacity = Math.Clamp( opacity, 0f, 1f );
	}
}

/// <summary>Rises in world space and fades out, then destroys the floater object.</summary>
public sealed class ThornsDamageFloaterAnimator : Component
{
	const float LifetimeSeconds = 0.82f;
	const float RiseSpeed = 38f;

	float _elapsed;
	ThornsDamageFloaterLabelPanel _panel;

	protected override void OnStart()
	{
		_panel = Components.Get<ThornsDamageFloaterLabelPanel>();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		_elapsed += Time.Delta;

		GameObject.WorldPosition += Vector3.Up * RiseSpeed * Time.Delta;

		if ( _panel.IsValid() )
		{
			var t = _elapsed / LifetimeSeconds;
			var fade = 1f - MathF.Pow( Math.Clamp( t, 0f, 1f ), 1.15f );
			_panel.SetOpacity( fade );
		}

		if ( _elapsed >= LifetimeSeconds )
			GameObject.Destroy();
	}
}
