namespace NoFly;

public static class AlertNet
{
	public static string ToJson( List<SecurityAlert> alerts )
	{
		if ( alerts is null || alerts.Count == 0 ) return "";
		return string.Join( "||", alerts.Select( a =>
			$"{a.Id}|{(int)a.Type}|{Sanitize( a.Message )}|{a.TargetPlayerId}|{a.SourcePlayerId}|{a.CreatedAt}|{(a.Resolved ? 1 : 0)}" ) );
	}

	public static List<SecurityAlert> FromJson( string json )
	{
		var list = new List<SecurityAlert>();
		if ( string.IsNullOrEmpty( json ) ) return list;
		foreach ( var part in json.Split( "||", StringSplitOptions.RemoveEmptyEntries ) )
		{
			var p = part.Split( '|' );
			if ( p.Length < 7 ) continue;
			list.Add( new SecurityAlert
			{
				Id = p[0],
				Type = (AlertType)int.Parse( p[1] ),
				Message = p[2].Replace( "\\n", "\n" ),
				TargetPlayerId = p[3],
				SourcePlayerId = p[4],
				CreatedAt = float.Parse( p[5] ),
				Resolved = p[6] == "1"
			} );
		}
		return list;
	}

	static string Sanitize( string s ) => (s ?? "").Replace( "|", "/" ).Replace( "||", "/" );
}

public static class ResultsNet
{
	public static string ToJson( RoundResults r )
	{
		if ( r is null ) return "";
		return string.Join( "~~",
			r.Headline,
			((int)r.Winner).ToString(),
			r.SmugglerName,
			r.UndercoverName,
			r.ForgedField,
			r.ContrabandHide,
			string.Join( "||", r.Lines ?? new() ),
			string.Join( "||", r.Mvps ?? new() ) );
	}

	public static RoundResults FromJson( string json )
	{
		if ( string.IsNullOrEmpty( json ) ) return null;
		var p = json.Split( "~~" );
		if ( p.Length < 8 ) return null;
		return new RoundResults
		{
			Headline = p[0],
			Winner = (WinSide)int.Parse( p[1] ),
			SmugglerName = p[2],
			UndercoverName = p[3],
			ForgedField = p[4],
			ContrabandHide = p[5],
			Lines = p[6].Split( "||", StringSplitOptions.RemoveEmptyEntries ).ToList(),
			Mvps = p[7].Split( "||", StringSplitOptions.RemoveEmptyEntries ).ToList()
		};
	}
}
