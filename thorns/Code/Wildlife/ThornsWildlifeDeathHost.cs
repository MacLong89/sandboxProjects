using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Host: halt locomotion on wildlife death, wait for death sequence duration, then destroy the entity.
/// </summary>
public sealed class ThornsWildlifeDeathHost : Component
{
	public const float FallbackDespawnDelaySeconds = 2.4f;
	public const float DespawnBufferSeconds = 0.25f;
	public const float MinDespawnDelaySeconds = 0.85f;
	public const float MaxDespawnDelaySeconds = 6f;

	bool _despawnStarted;

	public static void HostBeginDespawn( GameObject root )
	{
		if ( !Networking.IsHost || root is null || !root.IsValid() )
			return;

		var host = root.Components.Get<ThornsWildlifeDeathHost>();
		if ( !host.IsValid() )
			host = root.Components.Create<ThornsWildlifeDeathHost>();

		host.BeginDespawn();
	}

	void BeginDespawn()
	{
		if ( _despawnStarted || !Networking.IsHost )
			return;

		_despawnStarted = true;

		var brain = Components.Get<ThornsWildlifeBrain>();
		if ( brain.IsValid() )
		{
			brain.HostApplyDeathHold( Components.Get<ThornsWildlifeMotor>() );
			ThornsPopulationDirector.HostUnregisterWildlife( brain );
		}
		else
		{
			Components.Get<ThornsWildlifeMotor>()?.HostHaltPlanarLocomotion();
		}

		Components.Get<ThornsWildlifeAnimSync>()?.HostSetLocomotionPlanarSpeed( 0f );

		var cc = Components.Get<CharacterController>();
		if ( cc.IsValid() )
			cc.Enabled = false;

		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( id.IsValid() && id.WildlifeId != Guid.Empty
		     && ThornsWildlifeIdentity.ActiveByHost.TryGetValue( id.WildlifeId, out var existing )
		     && existing == id )
			ThornsWildlifeIdentity.ActiveByHost.Remove( id.WildlifeId );

		var delay = ResolveDespawnDelaySeconds( GameObject );
		_ = DestroyAfterDelayAsync( delay );
	}

	public static float ResolveDespawnDelaySeconds( GameObject root )
	{
		if ( root is null || !root.IsValid() )
			return FallbackDespawnDelaySeconds;

		var deathSeq = ResolveDeathSequenceName( root );
		var skin = root.Components.Get<SkinnedModelRenderer>( FindMode.EnabledInSelf );
		if ( skin.IsValid() && !string.IsNullOrWhiteSpace( deathSeq ) )
		{
			skin.UseAnimGraph = false;
			skin.Sequence.Name = deathSeq;
			var duration = skin.Sequence.Duration;
			if ( duration > 0.05f )
				return Math.Clamp( duration + DespawnBufferSeconds, MinDespawnDelaySeconds, MaxDespawnDelaySeconds );
		}

		return FallbackDespawnDelaySeconds;
	}

	static string ResolveDeathSequenceName( GameObject root )
	{
		foreach ( var elk in root.Components.GetAll<ThornsWildlifeElkAnimDriver>( FindMode.EnabledInSelf ) )
		{
			if ( elk.IsValid() && !string.IsNullOrWhiteSpace( elk.DeathSequenceName ) )
				return elk.DeathSequenceName;
		}

		foreach ( var panther in root.Components.GetAll<ThornsWildlifePantherAnimDriver>( FindMode.EnabledInSelf ) )
		{
			if ( panther.IsValid() && !string.IsNullOrWhiteSpace( panther.DeathSequenceName ) )
				return panther.DeathSequenceName;
		}

		return null;
	}

	async Task DestroyAfterDelayAsync( float delaySeconds )
	{
		await Task.DelayRealtimeSeconds( Math.Max( 0.05f, delaySeconds ) );
		if ( !GameObject.IsValid() )
			return;

		ThornsWildlifeLog.Despawn( GameObject.Name );
		GameObject.Destroy();
	}
}
