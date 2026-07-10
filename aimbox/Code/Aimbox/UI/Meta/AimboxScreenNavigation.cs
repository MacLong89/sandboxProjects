namespace Sandbox;

public static class AimboxScreenNavigation
{
	public static void GoBack()
	{
		if ( AimboxMetaNavigation.IsInIntermission )
		{
			if ( AimboxMetaNavigation.IsPostMatchFlow
			     && AimboxMetaNavigation.CurrentScreen == AimboxMetaScreen.Barracks )
				AimboxMetaNavigation.OpenScreen( AimboxMetaScreen.Scoreboard );
			else
				AimboxMetaNavigation.OpenScreen( AimboxMetaScreen.PostMatch );

			return;
		}

		AimboxMetaNavigation.Close();
	}

	public static bool TryHandleMenuBack( bool enabled )
	{
		if ( !enabled || !Input.Pressed( "Menu" ) )
			return false;

		GoBack();
		return true;
	}
}
