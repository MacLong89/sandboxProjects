namespace Sandbox;

public readonly record struct ThornsPlacementSocketBind( Guid InstanceGuid, ushort SocketIndex );

/// <summary>Authoritative mating face reservations — survives per structure instance until demolish (<see cref="ThornsPlacedStructure"/> teardown).</summary>
public static class ThornsBuildingSocketLedger
{
	static readonly HashSet<(Guid Id, ushort Socket)> _fills = new();
	static readonly Dictionary<Guid, List<ThornsPlacementSocketBind>> _reservationsByOccupant = new();

	public static bool HostIsFree( ThornsPlacementSocketBind bind ) =>
		!_fills.Contains( (bind.InstanceGuid, bind.SocketIndex) );

	public static bool HostTryOccupyPair(
		ThornsPlacementSocketBind a,
		ThornsPlacementSocketBind b,
		Guid occupantInstanceGuid )
	{
		if ( occupantInstanceGuid == Guid.Empty )
			return false;

		if ( _fills.Contains( (a.InstanceGuid, a.SocketIndex) ) ||
		     _fills.Contains( (b.InstanceGuid, b.SocketIndex) ) )
			return false;

		HostRegisterReservation( occupantInstanceGuid, a );
		HostRegisterReservation( occupantInstanceGuid, b );
		return true;
	}

	public static bool HostTryOccupy( ThornsPlacementSocketBind solo, Guid occupantInstanceGuid )
	{
		if ( occupantInstanceGuid == Guid.Empty )
			return false;

		if ( _fills.Contains( (solo.InstanceGuid, solo.SocketIndex) ) )
			return false;

		HostRegisterReservation( occupantInstanceGuid, solo );
		return true;
	}

	static void HostRegisterReservation( Guid occupantInstanceGuid, ThornsPlacementSocketBind bind )
	{
		if ( !_reservationsByOccupant.TryGetValue( occupantInstanceGuid, out var list ) )
		{
			list = new List<ThornsPlacementSocketBind>( 2 );
			_reservationsByOccupant[occupantInstanceGuid] = list;
		}

		list.Add( bind );
		_fills.Add( (bind.InstanceGuid, bind.SocketIndex) );
	}

	/// <summary>
	/// Releases every socket this structure occupied — including host-piece mates (foundation edges, wall seats, etc.).
	/// </summary>
	public static void HostClearInstance( Guid instanceGuid )
	{
		if ( instanceGuid == Guid.Empty )
			return;

		if ( !_reservationsByOccupant.TryGetValue( instanceGuid, out var list ) )
		{
			_fills.RemoveWhere( kv => kv.Id == instanceGuid );
			return;
		}

		foreach ( var bind in list )
			_fills.Remove( (bind.InstanceGuid, bind.SocketIndex) );

		_reservationsByOccupant.Remove( instanceGuid );
	}
}
