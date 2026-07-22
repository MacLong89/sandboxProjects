namespace Offshore;

public sealed class ObjectiveService
{
	public int Index { get; private set; }
	public int Progress { get; private set; }
	public bool AllComplete => Index >= Catalog.Objectives.Count;

	public ObjectiveDefinition Current => AllComplete ? null : Catalog.Objectives[Index];

	public string HudText
	{
		get
		{
			if ( AllComplete )
				return "All intro goals complete";
			var o = Current;
			if ( o.Target > 1 )
				return $"{o.Title} ({Progress}/{o.Target})";
			return o.Title;
		}
	}

	public void Load( SaveData save )
	{
		Index = Math.Clamp( save.ObjectiveIndex, 0, Catalog.Objectives.Count );
		Progress = save.ObjectiveProgress;
	}

	public void SyncTo( SaveData save )
	{
		save.ObjectiveIndex = Index;
		save.ObjectiveProgress = Progress;
	}

	public bool Notify( string eventKey, int amount = 1 )
	{
		if ( AllComplete )
			return false;
		var o = Current;
		if ( o.EventKey != eventKey )
			return false;

		Progress += amount;
		if ( Progress >= o.Target )
		{
			Progress = 0;
			Index++;
			return true;
		}
		return false;
	}
}
