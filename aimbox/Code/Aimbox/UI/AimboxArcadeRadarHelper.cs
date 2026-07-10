namespace Sandbox;

public readonly struct AimboxRadarBlip
{
	public float NormalizedX { get; init; }
	public float NormalizedY { get; init; }
	public bool IsHostile { get; init; }
	public bool IsTeammate { get; init; }
}

public static class AimboxArcadeRadarHelper
{
	public static List<AimboxRadarBlip> BuildContacts( AimboxPlayerController player, AimboxGame game )
	{
		var contacts = new List<AimboxRadarBlip>();
		if ( player is null || game is null )
			return contacts;

		var origin = player.WorldPosition;
		var forward = player.EyeRotation.Forward.WithZ( 0 );
		if ( forward.Length <= 0.001f )
			forward = Vector3.Forward;
		forward = forward.Normal;

		var right = player.EyeRotation.Right.WithZ( 0 );
		if ( right.Length <= 0.001f )
			right = Vector3.Right;
		right = right.Normal;

		var uavActive = game.Killstreaks.IsUavActive( player.AccountId );
		var range = AimboxArcadeHudUi.RadarRange;

		foreach ( var actor in game.GetAllCombatActors() )
		{
			if ( actor == player || !actor.IsAlive )
				continue;

			if ( !uavActive && actor.WorldPosition.Distance( origin ) > range )
				continue;

			var delta = actor.WorldPosition - origin;
			delta = delta.WithZ( 0 );
			if ( delta.Length <= 1f )
				continue;

			var localX = Vector3.Dot( delta, right );
			var localY = Vector3.Dot( delta, forward );

			contacts.Add( new AimboxRadarBlip
			{
				NormalizedX = Math.Clamp( localX / range, -1f, 1f ),
				NormalizedY = Math.Clamp( localY / range, -1f, 1f ),
				IsHostile = !player.IsTeammate( actor ),
				IsTeammate = player.IsTeammate( actor )
			} );
		}

		return contacts;
	}
}
