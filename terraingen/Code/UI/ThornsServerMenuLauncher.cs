namespace Terraingen.UI;

using System.Linq;
using Terraingen.UI.Menu;

/// <summary>Fallback main-menu bootstrap when <see cref="ThornsMenuSceneBootstrap"/> is missing from the scene.</summary>
[Title( "Thorns Server Menu Launcher" )]
[Category( "Thorns/UI" )]
[Icon( "dns" )]
public sealed class ThornsServerMenuLauncher : Component
{
	[Property] public bool CreateUiOnStart { get; set; } = true;

	protected override void OnStart()
	{
		if ( !CreateUiOnStart )
			return;

		if ( Scene.GetAllComponents<ThornsMenuSceneBootstrap>().Any( b => b.IsValid ) )
			return;

		Log.Info( "[Thorns Menu] Launcher fallback — creating menu UI." );
		_ = ThornsMainMenuBootstrap.EnsureMenuUiAsync( GameObject, Scene );
	}
}
