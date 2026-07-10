namespace Sandbox;

/// <summary>Runs after gameplay/UI components so cursor/input state matches session phase.</summary>
[Title( "Aimbox Cursor Guard" )]
[Category( "Aimbox" )]
[Order( 10000 )]
public sealed class AimboxCursorGuard : Component
{
	protected override void OnUpdate()
	{
		AimboxCursor.Sync();
	}

	protected override void OnFixedUpdate()
	{
		if ( !AimboxMetaNavigation.RequiresMouseCursor() )
			AimboxCursor.Sync();
	}

	protected override void OnPreRender()
	{
		AimboxCursor.Sync();
	}
}
