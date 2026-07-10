namespace Sandbox;

/// <summary>Shared combat pawn contract for human players and AI bots.</summary>
public interface IAimboxCombatActor
{
	GameObject GameObject { get; }
	Scene Scene { get; }
	Vector3 WorldPosition { get; set; }
	Rotation WorldRotation { get; set; }
	Vector3 EyePosition { get; }
	Vector3 AimForward { get; }
	Rotation EyeRotation { get; }
	bool IsAlive { get; }
	AimboxTeam Team { get; }
	string CombatId { get; }
	bool IsHumanPlayer { get; }
	bool IsCrouching { get; }
	bool IsSprintMoving { get; }
	bool ShowThirdPersonBody { get; }
	Vector3 GetMovementVelocity();
	float GetCombatPitch();
	void SetCombatPitch( float pitch );
	AimboxWeaponId ActiveWeapon { get; }
	AimboxWeaponRuntime CurrentWeapon { get; }
	bool IsTeammate( IAimboxCombatActor other );
	void TakeDamage( IAimboxCombatActor attacker, AimboxWeaponId weapon, float damage, bool headshot, float distance );
	void ConfirmKill( IAimboxCombatActor victim, AimboxWeaponId weapon, bool headshot, float distance );
	void RegisterCombatHitFeedback( float damage, bool headshot );
}

public static class AimboxCombatActorRegistry
{
	public static IEnumerable<IAimboxCombatActor> GetAll( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			yield break;

		foreach ( var player in scene.GetAllComponents<AimboxPlayerController>() )
		{
			if ( player.IsValid() )
				yield return player;
		}

		foreach ( var bot in scene.GetAllComponents<AimboxBotController>() )
		{
			if ( bot.IsValid() )
				yield return bot;
		}
	}

	public static IAimboxCombatActor FindFromGameObject( GameObject hitObject )
	{
		if ( !hitObject.IsValid() )
			return null;

		var player = hitObject.Components.Get<AimboxPlayerController>( FindMode.EverythingInSelfAndParent );
		if ( player is not null )
			return player;

		return hitObject.Components.Get<AimboxBotController>( FindMode.EverythingInSelfAndParent );
	}
}
