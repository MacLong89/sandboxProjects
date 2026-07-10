using System.Linq;

namespace Sandbox;

/// <summary>
/// During <see cref="YaGameState.InRound"/>, dead players or mid-round joiners (unassigned role) spectate alive participants.
/// <see cref="Input.Pressed"/> Attack1 / Attack2 cycle targets; camera follows their approximate eye.
/// </summary>
[Title( "YouAreNotAlone — Round spectator" )]
[Category( "YouAreNotAlone" )]
[Icon( "visibility" )]
[Order( 110 )]
public sealed class YaRoundSpectator : Component
{
	static readonly Vector3 FallbackEyeOffsetLocal = new( 0f, 0f, 52f );

	/// <summary>True when local pawn should not move or look (mid-round join before next role assignment).</summary>
	public static bool LocalMidRoundJoinFreeze( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;
		var gs = YaGameStateSystem.Instance;
		if ( gs is null || !gs.IsValid() || gs.CurrentState != YaGameState.InRound )
			return false;
		var roleCmp = pawnRoot.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var role = roleCmp.IsValid() ? roleCmp.Role : YaPlayerRole.Unassigned;
		return role == YaPlayerRole.Unassigned;
	}

	/// <summary>Dead during a round, or alive but unassigned while the match is in round (late join).</summary>
	public static bool LocalShouldSpectateOthers( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;
		var gs = YaGameStateSystem.Instance;
		if ( gs is null || !gs.IsValid() || gs.CurrentState != YaGameState.InRound )
			return false;
		var hp = pawnRoot.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		var roleCmp = pawnRoot.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var role = roleCmp.IsValid() ? roleCmp.Role : YaPlayerRole.Unassigned;
		if ( hp.IsValid() && hp.IsDeadState )
			return true;
		return role == YaPlayerRole.Unassigned;
	}

	static bool IsSpectatableAliveTarget( GameObject otherRoot, GameObject localRoot )
	{
		if ( otherRoot is null || !otherRoot.IsValid() || otherRoot == localRoot )
			return false;
		var role = YaTeamSystem.GetRole( otherRoot );
		if ( role != YaPlayerRole.Alone && role != YaPlayerRole.NotAlone )
			return false;
		var hp = otherRoot.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		return hp is { IsValid: true, IsAlive: true } && !hp.IsDeadState;
	}

	static bool TryGetSpectateWorldTransform( GameObject targetRoot, out Vector3 worldPos, out Rotation worldRot )
	{
		worldPos = default;
		worldRot = default;
		if ( targetRoot is null || !targetRoot.IsValid() )
			return false;

		var view = targetRoot.Children.FirstOrDefault( c => c.IsValid() && c.Name == "View" );
		var eyeLocal = FallbackEyeOffsetLocal;
		if ( view is not null && view.IsValid() )
		{
			var cam = view.Components.Get<YaPawnCamera>( FindMode.EnabledInSelf );
			if ( cam.IsValid() )
				eyeLocal = cam.EyeOffsetLocal;
		}

		worldPos = targetRoot.WorldPosition + targetRoot.WorldRotation * eyeLocal;
		worldRot = targetRoot.WorldRotation;
		return true;
	}

	int _targetIndex;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		var pawnRoot = GameObject.Parent;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return;

		var vm = Components.Get<YaViewModelController>( FindMode.EnabledInSelf );
		if ( !LocalShouldSpectateOthers( pawnRoot ) )
		{
			vm?.SetSpectatorPresentationHidden( false );
			return;
		}

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
		{
			vm?.SetSpectatorPresentationHidden( false );
			return;
		}

		var targets = YaTeamSystem.EnumeratePlayerRoots( scene )
			.Where( r => IsSpectatableAliveTarget( r, pawnRoot ) )
			.OrderBy( r => r.Network.OwnerId )
			.ToArray();

		if ( targets.Length == 0 )
		{
			vm?.SetSpectatorPresentationHidden( false );
			return;
		}

		vm?.SetSpectatorPresentationHidden( true );

		if ( _targetIndex >= targets.Length )
			_targetIndex = 0;

		if ( Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" ) )
			_targetIndex = ( _targetIndex + 1 ) % targets.Length;
		if ( Input.Pressed( "Attack2" ) || Input.Pressed( "attack2" ) )
			_targetIndex = ( _targetIndex + targets.Length - 1 ) % targets.Length;

		var tgt = targets[_targetIndex];
		if ( tgt is null || !tgt.IsValid() )
			return;

		if ( !TryGetSpectateWorldTransform( tgt, out var pos, out var rot ) )
			return;

		GameObject.WorldPosition = pos;
		GameObject.WorldRotation = rot;
	}
}
