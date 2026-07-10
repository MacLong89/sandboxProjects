using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host registry for active supply beacons — avoids per-player <see cref="Scene.GetAllComponents{T}"/> in music suppression.
/// </summary>
public static class ThornsDynamicSupplyBeaconPopulation
{
	static readonly List<ThornsDynamicSupplyBeacon> Beacons = new();

	public static void HostRegister( ThornsDynamicSupplyBeacon beacon )
	{
		if ( !Networking.IsHost || beacon is null || !beacon.IsValid() )
			return;

		if ( !Beacons.Contains( beacon ) )
			Beacons.Add( beacon );
	}

	public static void HostUnregister( ThornsDynamicSupplyBeacon beacon )
	{
		if ( beacon is null )
			return;

		Beacons.Remove( beacon );
	}

	public static IReadOnlyList<ThornsDynamicSupplyBeacon> HostBeaconsReadOnly => Beacons;
}
