using Sandbox.UI;

namespace Fauna2.UI;

/// <summary>Plays UI click SFX for mouse clicks on interactive panels.</summary>
public sealed class UiClickSounds : Component
{
	private readonly HashSet<Panel> _hooked = new();
	private int _idleFrames;

	protected override void OnUpdate()
	{
		if ( _idleFrames > 120 )
			return;

		_hooked.RemoveWhere( panel => !panel.IsValid() );

		var hookedBefore = _hooked.Count;

		foreach ( var panelComponent in Scene.GetAllComponents<PanelComponent>() )
		{
			var root = panelComponent.Panel;
			if ( root is null || !root.IsValid() )
				continue;

			TryHook( root );

			foreach ( var node in root.Descendants )
				TryHook( node );
		}

		_idleFrames = _hooked.Count == hookedBefore ? _idleFrames + 1 : 0;
	}

	private void TryHook( Panel panel )
	{
		if ( !panel.WantsMouseInput() || !_hooked.Add( panel ) )
			return;

		panel.AddEventListener( "onclick", _ => UiState.PlayClick() );
	}
}
