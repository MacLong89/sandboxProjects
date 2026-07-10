namespace Sandbox;

/// <summary>Matches saved <see cref="ThornsPlacedStructure.OwnerAccountKeySync"/> against live <see cref="Connection"/> steam/local keys.</summary>
public static class ThornsStructureOwnership
{
	public static bool HostCallerOwnsStructure( Guid callerConnectionId, ThornsPlacedStructure ps )
	{
		if ( ps is null || !ps.IsValid() )
			return false;

		if ( ps.OwnerConnectionId == callerConnectionId )
			return true;

		var conn = Connection.Find( callerConnectionId );
		if ( conn is null )
			return false;

		var key = ThornsPersistenceIdentity.GetStableAccountKey( conn );
		return !string.IsNullOrEmpty( ps.OwnerAccountKeySync ) && ps.OwnerAccountKeySync == key;
	}
}
