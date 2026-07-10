namespace UnderPressure;

/// <summary>Developer console shortcuts for playtesting.</summary>
public static class DevCommands
{
	[ConCmd( "up_complete" )]
	public static void CompleteJob()
	{
		var core = GameCore.Instance;
		if ( core is null )
		{
			Log.Warning( "[up_complete] GameCore is not running." );
			return;
		}

		core.CheatInstantComplete();
	}

	[ConCmd( "up_reset" )]
	public static void ResetProgress()
	{
		var core = GameCore.Instance;
		if ( core is null )
		{
			Log.Warning( "[up_reset] GameCore is not running." );
			return;
		}

		core.ResetAllProgress();
	}

	[ConCmd( "up_level" )]
	public static void JumpToLevel( int level = 1 )
	{
		var core = GameCore.Instance;
		if ( core is null )
		{
			Log.Warning( "[up_level] GameCore is not running." );
			return;
		}

		core.CheatJumpToLevel( level );
	}

	[ConCmd( "up_fixer" )]
	public static void ResetFixerBriefing()
	{
		var core = GameCore.Instance;
		if ( core is null )
		{
			Log.Warning( "[up_fixer] GameCore is not running." );
			return;
		}

		core.CheatResetFixerBriefing();
	}
}
