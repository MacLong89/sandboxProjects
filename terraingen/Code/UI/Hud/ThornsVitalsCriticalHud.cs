namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Hunger/thirst critical edge pulse + optional heartbeat feel via opacity.</summary>
public sealed class ThornsVitalsCriticalHud
{
	readonly Panel _edge;
	double _pulseUntil;

	public ThornsVitalsCriticalHud( Panel parent )
	{
		_edge = ThornsUiFactory.AddPanel( parent, "vitals-critical-edge" );
		_edge.Style.Position = PositionMode.Absolute;
		_edge.Style.Left = Length.Pixels( 0 );
		_edge.Style.Top = Length.Pixels( 0 );
		_edge.Style.Width = Length.Percent( 100 );
		_edge.Style.Height = Length.Percent( 100 );
		_edge.Style.BorderWidth = Length.Pixels( 0 );
		_edge.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.ApplyPassive( _edge, ThornsUiPriority.PassiveOverlay );
		_edge.Style.Display = DisplayMode.None;
	}

	public void Refresh()
	{
		if ( !_edge.IsValid || !ThornsUiClientState.HasSnapshot )
			return;

		var v = ThornsUiClientState.Snapshot.Vitals ?? new();
		var foodLow = v.ShowFood && v.MaxFood > 0.01f && v.Food / v.MaxFood <= 0.22f;
		var waterLow = v.ShowWater && v.MaxWater > 0.01f && v.Water / v.MaxWater <= 0.22f;
		var critical = foodLow || waterLow;

		if ( critical )
			_pulseUntil = Time.Now + 0.35;

		var pulse = Time.Now < _pulseUntil;
		if ( !critical && !pulse )
		{
			_edge.Style.Display = DisplayMode.None;
			return;
		}

		var alpha = critical ? 0.35f + 0.25f * MathF.Sin( (float)Time.Now * 8f ) : 0.15f;
		_edge.Style.Display = DisplayMode.Flex;
		_edge.Style.BorderWidth = Length.Pixels( 6 );
		_edge.Style.BorderColor = (waterLow ? new Color( 0.2f, 0.45f, 0.95f ) : new Color( 0.85f, 0.35f, 0.15f ) ).WithAlpha( alpha );
	}
}
