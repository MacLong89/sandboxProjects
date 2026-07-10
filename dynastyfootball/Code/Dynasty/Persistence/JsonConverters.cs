using System.Text.Json;

using System.Text.Json.Serialization;

using Dynasty.Core.Identifiers;



namespace Dynasty.Persistence;



public sealed class TeamIdJsonConverter : JsonConverter<TeamId>

{

	public override TeamId Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void Write( Utf8JsonWriter writer, TeamId value, JsonSerializerOptions options )

		=> writer.WriteStringValue( Format( value.Value ) );



	public override TeamId ReadAsPropertyName( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void WriteAsPropertyName( Utf8JsonWriter writer, TeamId value, JsonSerializerOptions options )

		=> writer.WritePropertyName( Format( value.Value ) );



	static TeamId Parse( string value ) => TeamId.From( EntityId.From( Guid.Parse( value ) ) );



	static string Format( Guid value ) => value.ToString( "N" );

}



public sealed class PlayerIdJsonConverter : JsonConverter<PlayerId>

{

	public override PlayerId Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void Write( Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options )

		=> writer.WriteStringValue( Format( value.Value ) );



	public override PlayerId ReadAsPropertyName( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void WriteAsPropertyName( Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options )

		=> writer.WritePropertyName( Format( value.Value ) );



	static PlayerId Parse( string value ) => PlayerId.From( EntityId.From( Guid.Parse( value ) ) );



	static string Format( Guid value ) => value.ToString( "N" );

}



public sealed class CoachIdJsonConverter : JsonConverter<CoachId>

{

	public override CoachId Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void Write( Utf8JsonWriter writer, CoachId value, JsonSerializerOptions options )

		=> writer.WriteStringValue( Format( value.Value ) );



	public override CoachId ReadAsPropertyName( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void WriteAsPropertyName( Utf8JsonWriter writer, CoachId value, JsonSerializerOptions options )

		=> writer.WritePropertyName( Format( value.Value ) );



	static CoachId Parse( string value ) => CoachId.From( EntityId.From( Guid.Parse( value ) ) );



	static string Format( Guid value ) => value.ToString( "N" );

}



public sealed class LeagueIdJsonConverter : JsonConverter<LeagueId>

{

	public override LeagueId Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void Write( Utf8JsonWriter writer, LeagueId value, JsonSerializerOptions options )

		=> writer.WriteStringValue( Format( value.Value ) );



	public override LeagueId ReadAsPropertyName( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void WriteAsPropertyName( Utf8JsonWriter writer, LeagueId value, JsonSerializerOptions options )

		=> writer.WritePropertyName( Format( value.Value ) );



	static LeagueId Parse( string value ) => LeagueId.From( EntityId.From( Guid.Parse( value ) ) );



	static string Format( Guid value ) => value.ToString( "N" );

}



public sealed class GameIdJsonConverter : JsonConverter<GameId>

{

	public override GameId Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void Write( Utf8JsonWriter writer, GameId value, JsonSerializerOptions options )

		=> writer.WriteStringValue( Format( value.Value ) );



	public override GameId ReadAsPropertyName( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void WriteAsPropertyName( Utf8JsonWriter writer, GameId value, JsonSerializerOptions options )

		=> writer.WritePropertyName( Format( value.Value ) );



	static GameId Parse( string value ) => new( Guid.Parse( value ) );



	static string Format( Guid value ) => value.ToString( "N" );

}



public sealed class DraftPickIdJsonConverter : JsonConverter<DraftPickId>

{

	public override DraftPickId Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void Write( Utf8JsonWriter writer, DraftPickId value, JsonSerializerOptions options )

		=> writer.WriteStringValue( Format( value.Value ) );



	public override DraftPickId ReadAsPropertyName( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )

		=> Parse( reader.GetString() );



	public override void WriteAsPropertyName( Utf8JsonWriter writer, DraftPickId value, JsonSerializerOptions options )

		=> writer.WritePropertyName( Format( value.Value ) );



	static DraftPickId Parse( string value ) => new( Guid.Parse( value ) );



	static string Format( Guid value ) => value.ToString( "N" );

}


