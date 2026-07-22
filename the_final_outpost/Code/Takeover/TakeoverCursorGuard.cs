namespace FinalOutpost;

/// <summary>Runs late so cursor state wins over ScreenPanel / HUD updates.</summary>
[Title( "Takeover Cursor Guard" )]
[Category( "Final Outpost" )]
[Order( 10000 )]
public sealed class TakeoverCursorGuard : Component
{
	protected override void OnUpdate() => TakeoverCursor.Sync();

	protected override void OnFixedUpdate()
	{
		if ( RecruitTakeoverController.Instance?.IsPossessing == true )
			TakeoverCursor.Sync();
	}

	protected override void OnPreRender() => TakeoverCursor.Sync();
}
