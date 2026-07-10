namespace Terraingen.UI.Menu;

/// <summary>Dismisses the engine loading overlay (Title=null alone leaves a stuck black screen).</summary>
public static class ThornsLoadingScreenUtil
{
	public static void Dismiss()
	{
		LoadingScreen.Title = null;
		LoadingScreen.Subtitle = null;
		LoadingScreen.IsVisible = false;
	}
}
