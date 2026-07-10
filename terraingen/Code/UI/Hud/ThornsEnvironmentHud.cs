namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.World.Environment;

public sealed class ThornsEnvironmentHud
{
	readonly Label _temp;
	readonly Label _weather;

	public ThornsEnvironmentHud( Panel parent )
	{
		var root = ThornsUiFactory.AddPanel( parent, "environment-hud" );
		root.Style.Position = PositionMode.Absolute;
		root.Style.Left = Length.Pixels( 20 );
		root.Style.Bottom = Length.Pixels( 96 );
		root.Style.FlexDirection = FlexDirection.Column;

		ThornsUiFactory.AddLabel( root, "Press [ENTER] to chat", "hud-chat-hint" );

		var row = ThornsUiFactory.AddPanel( root, "hud-env-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.MarginTop = Length.Pixels( 6 );

		ThornsUiFactory.AddLabel( row, "☀", "hud-env-icon" );
		_temp = ThornsUiFactory.AddLabel( row, "18°C", "hud-env-text" );
		ThornsUiFactory.AddLabel( row, "☁", "hud-env-icon" );
		_weather = ThornsUiFactory.AddLabel( row, "PARTLY CLOUDY", "hud-env-text" );

		Refresh();
	}

	public void Refresh()
	{
		var v = ThornsUiClientState.Snapshot.Vitals;
		_temp.Text = $"{v.TemperatureC:0}°C";

		var scene = Game.ActiveScene;
		if ( scene is not null && ThornsTimeOfDaySystem.TryGet( scene, out var time ) && time.IsValid() )
		{
			var state = time.CurrentState;
			_weather.Text = state.NightFactor > 0.65f ? "CLEAR NIGHT" : state.CloudOpacity > 0.22f ? "PARTLY CLOUDY" : "CLEAR";
		}
		else
			_weather.Text = "PARTLY CLOUDY";
	}
}
