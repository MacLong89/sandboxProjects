namespace PawnShop;

/// <summary>Tracks history with recurring named customers.</summary>
public sealed class RelationshipSystem
{
	private readonly SaveData _save;

	public RelationshipSystem( SaveData save )
	{
		_save = save;
	}

	public RelationshipData Get( string customerId, string name )
	{
		if ( string.IsNullOrEmpty( customerId ) ) return null;

		if ( !_save.Relationships.TryGetValue( customerId, out var rel ) || rel is null )
		{
			rel = new RelationshipData { CustomerId = customerId, Name = name };
			_save.Relationships[customerId] = rel;
		}

		return rel;
	}

	public IEnumerable<RelationshipData> All => _save.Relationships.Values.OrderByDescending( r => r.Deals );

	public void RecordDeal( string customerId, string name, int value, bool fair, bool lowball )
	{
		var rel = Get( customerId, name );
		if ( rel is null ) return;

		rel.Deals++;
		rel.TotalValue += Math.Abs( value );
		if ( fair ) { rel.FairDeals++; rel.Trust = Math.Min( 1f, rel.Trust + 0.08f ); }
		if ( lowball ) { rel.Lowballs++; rel.Trust = Math.Max( 0f, rel.Trust - 0.12f ); }
	}

	public void RecordPawnOutcome( string customerId, string name, bool redeemed )
	{
		var rel = Get( customerId, name );
		if ( rel is null ) return;

		if ( redeemed ) { rel.PawnsRedeemed++; rel.Trust = Math.Min( 1f, rel.Trust + 0.05f ); }
		else rel.PawnsDefaulted++;
	}
}
