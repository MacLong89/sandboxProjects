namespace NoFly;

public static class ObjectiveNet
{
	public static string ToJson( List<PlayerObjective> list )
	{
		if ( list is null || list.Count == 0 ) return "";
		return string.Join( "||", list.Select( o => $"{o.Id}|{o.Label}|{o.Hint}|{o.ZoneTag}|{o.Score}|{(o.Completed ? 1 : 0)}" ) );
	}

	public static List<PlayerObjective> FromJson( string json )
	{
		var list = new List<PlayerObjective>();
		if ( string.IsNullOrEmpty( json ) ) return list;
		foreach ( var part in json.Split( "||", StringSplitOptions.RemoveEmptyEntries ) )
		{
			var p = part.Split( '|' );
			if ( p.Length < 6 ) continue;
			list.Add( new PlayerObjective
			{
				Id = p[0],
				Label = p[1],
				Hint = p[2],
				ZoneTag = p[3],
				Score = int.Parse( p[4] ),
				Completed = p[5] == "1"
			} );
		}
		return list;
	}
}
