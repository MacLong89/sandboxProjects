namespace Dynasty.Core.Identifiers;

/// <summary>
/// Strongly-typed entity identifier. Serializable and stable across saves and network snapshots.
/// </summary>
public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
{
	public Guid Value { get; }

	public EntityId( Guid value ) => Value = value;

	public static EntityId New() => new( Guid.NewGuid() );

	public static EntityId From( Guid value ) => new( value );

	public static EntityId From( string value ) => new( Guid.Parse( value ) );

	public static EntityId Empty => new( Guid.Empty );

	public bool IsEmpty => Value == Guid.Empty;

	public bool Equals( EntityId other ) => Value.Equals( other.Value );

	public override bool Equals( object obj ) => obj is EntityId other && Equals( other );

	public override int GetHashCode() => Value.GetHashCode();

	public int CompareTo( EntityId other ) => Value.CompareTo( other.Value );

	public override string ToString() => Value.ToString( "N" );

	public static bool operator ==( EntityId left, EntityId right ) => left.Equals( right );

	public static bool operator !=( EntityId left, EntityId right ) => !left.Equals( right );
}

public readonly struct LeagueId : IEquatable<LeagueId>
{
	public Guid Value { get; }

	public LeagueId( Guid value ) => Value = value;

	public static LeagueId New() => new( Guid.NewGuid() );

	public static LeagueId From( EntityId id ) => new( id.Value );

	public bool IsEmpty => Value == Guid.Empty;

	public bool Equals( LeagueId other ) => Value.Equals( other.Value );

	public override bool Equals( object obj ) => obj is LeagueId other && Equals( other );

	public override int GetHashCode() => Value.GetHashCode();

	public override string ToString() => Value.ToString( "N" );

	public static implicit operator EntityId( LeagueId id ) => EntityId.From( id.Value );
}

public readonly struct TeamId : IEquatable<TeamId>
{
	public Guid Value { get; }

	public TeamId( Guid value ) => Value = value;

	public static TeamId New() => new( Guid.NewGuid() );

	public static TeamId From( EntityId id ) => new( id.Value );

	public static TeamId Empty => new( Guid.Empty );

	public bool IsEmpty => Value == Guid.Empty;

	public bool Equals( TeamId other ) => Value.Equals( other.Value );

	public override bool Equals( object obj ) => obj is TeamId other && Equals( other );

	public override int GetHashCode() => Value.GetHashCode();

	public override string ToString() => Value.ToString( "N" );

	public static implicit operator EntityId( TeamId id ) => EntityId.From( id.Value );
}

public readonly struct PlayerId : IEquatable<PlayerId>
{
	public Guid Value { get; }

	public PlayerId( Guid value ) => Value = value;

	public static PlayerId New() => new( Guid.NewGuid() );

	public static PlayerId From( EntityId id ) => new( id.Value );

	public static PlayerId Empty => new( Guid.Empty );

	public bool IsEmpty => Value == Guid.Empty;

	public bool Equals( PlayerId other ) => Value.Equals( other.Value );

	public override bool Equals( object obj ) => obj is PlayerId other && Equals( other );

	public override int GetHashCode() => Value.GetHashCode();

	public override string ToString() => Value.ToString( "N" );

	public static implicit operator EntityId( PlayerId id ) => EntityId.From( id.Value );
}

public readonly struct CoachId : IEquatable<CoachId>
{
	public Guid Value { get; }

	public CoachId( Guid value ) => Value = value;

	public static CoachId New() => new( Guid.NewGuid() );

	public static CoachId From( EntityId id ) => new( id.Value );

	public bool IsEmpty => Value == Guid.Empty;

	public bool Equals( CoachId other ) => Value.Equals( other.Value );

	public override bool Equals( object obj ) => obj is CoachId other && Equals( other );

	public override int GetHashCode() => Value.GetHashCode();

	public override string ToString() => Value.ToString( "N" );

	public static implicit operator EntityId( CoachId id ) => EntityId.From( id.Value );
}

public readonly struct GameId : IEquatable<GameId>
{
	public Guid Value { get; }

	public GameId( Guid value ) => Value = value;

	public static GameId New() => new( Guid.NewGuid() );

	public bool IsEmpty => Value == Guid.Empty;

	public bool Equals( GameId other ) => Value.Equals( other.Value );

	public override bool Equals( object obj ) => obj is GameId other && Equals( other );

	public override int GetHashCode() => Value.GetHashCode();

	public override string ToString() => Value.ToString( "N" );
}

public readonly struct DraftPickId : IEquatable<DraftPickId>
{
	public Guid Value { get; }

	public DraftPickId( Guid value ) => Value = value;

	public static DraftPickId New() => new( Guid.NewGuid() );

	public bool IsEmpty => Value == Guid.Empty;

	public bool Equals( DraftPickId other ) => Value.Equals( other.Value );

	public override bool Equals( object obj ) => obj is DraftPickId other && Equals( other );

	public override int GetHashCode() => Value.GetHashCode();

	public override string ToString() => Value.ToString( "N" );
}
