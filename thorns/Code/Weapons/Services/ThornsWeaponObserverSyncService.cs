#nullable disable

using System;
using System.Collections.Generic;

namespace Sandbox;

public sealed class ThornsWeaponObserverSyncService
{
	static readonly Dictionary<Guid, double> NextObserverGunshotRpcByShooter = new();

	readonly IThornsWeaponFxHost _host;

	public ThornsWeaponObserverSyncService( IThornsWeaponFxHost host ) => _host = host;

	public void PlayOwnerWeaponSoundAtEar( string resourcePath, float volumeMultiplier = 1f )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		var path = resourcePath.Trim();
		var h = ThornsCombatAuthority.TryGetAuthoritativeEye( _host.GameObject, out var ear, out _ )
			? Sound.Play( path, ear )
			: Sound.Play( path, _host.GameObject.WorldPosition );
		if ( Math.Abs( volumeMultiplier - 1f ) > 0.0001f )
			h.Volume = Math.Clamp( volumeMultiplier, 0f, 4f );
	}

	/// <summary>Host → owner: reload ticks / pump shells (async host reload).</summary>
	public void SendOwnerWeaponSound( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || !Networking.IsHost )
			return;

		_host.Weapon.RpcPlayOwnerWeaponSound( resourcePath.Trim() );
	}

	public void RpcPlayOwnerWeaponSound( string resourcePath ) => PlayOwnerWeaponSoundAtEar( resourcePath );

	/// <summary>Observers hear gunfire at the shooter's eye; the owner's client skips this (ear mix from <see cref="ThornsWeapon.RpcFireOutcome"/>).</summary>
	public void RpcObserversPlayerGunWorldShot( Guid shooterOwnerConnectionId, string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || !_host.GameObject.IsValid() )
			return;

		var lc = Connection.Local;
		if ( lc is not null && lc.Id == shooterOwnerConnectionId )
			return;

		var eye = _host.GameObject.WorldPosition + Vector3.Up * 64f;
		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( _host.GameObject, out var eyePos, out _ ) )
			eye = eyePos;

		if ( !ThornsWorldSpatialSfx.LocalListenerWithinPlanarRadius(
			     eye,
			     ThornsPerformanceBudgets.ObserverGunshotMaxHearingRadius ) )
			return;

		var localOffset = ThornsWorldSpatialSfx.WorldEmitToLocalOffset( _host.GameObject, eye );
		ThornsWorldSpatialSfx.PlayWorldOneShotFollowing(
			_host.GameObject,
			localOffset,
			resourcePath.Trim(),
			ThornsSpatialSfxCategory.PlayerGunshot );
	}

	public void HostMaybeBroadcastObserverGunshot( Guid shooterOwnerConnectionId, string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		var minInterval = 1f / MathF.Max( 4f, ThornsPerformanceBudgets.ObserverPlayerGunshotMaxRpcHz );
		if ( NextObserverGunshotRpcByShooter.TryGetValue( shooterOwnerConnectionId, out var nextAllowed )
		     && Time.Now < nextAllowed )
			return;

		NextObserverGunshotRpcByShooter[shooterOwnerConnectionId] = Time.Now + minInterval;
		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( _host.GameObject, out var eyePos, out _ ) )
			ThornsMusicWorldSignals.HostRegisterGunshot( eyePos );
		else
			ThornsMusicWorldSignals.HostRegisterGunshot( _host.GameObject.WorldPosition );
		_host.Weapon.RpcObserversPlayerGunWorldShot( shooterOwnerConnectionId, resourcePath );
	}
}
