namespace Sandbox;

/// <summary>Transient feedback zones — one visible message per zone, no center-screen stacking.</summary>
public enum YaUiPopupZone
{
	/// <summary>Heals and resource gains — anchored above bottom-left health HUD.</summary>
	BottomLeftPersonal,
	/// <summary>Kill confirms, ability callouts, challenges — upper-right under kill feed.</summary>
	TopRightCombat
}

/// <summary>Queues transient feedback — one visible popup per <see cref="YaUiPopupZone"/>.</summary>
public sealed class YaUiPopupQueue
{
	struct PopupEntry
	{
		public string Text;
		public Color Color;
		public float Remaining;
		public int Count;
	}

	sealed class ZoneQueue
	{
		public readonly List<PopupEntry> Queue = new();
		public readonly List<PopupEntry> Visible = new();
	}

	readonly Dictionary<YaUiPopupZone, ZoneQueue> _zones = new();

	const int MaxVisiblePerZone = 1;
	const float DefaultDuration = 2.2f;
	const float MergeWindowSeconds = 0.35f;

	public void Push( string text, Color color, YaUiPopupZone zone, float duration = DefaultDuration )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
			return;

		var bucket = GetZone( zone );

		if ( bucket.Visible.Count > 0
		     && bucket.Visible[^1].Text == text
		     && bucket.Visible[^1].Color == color
		     && bucket.Visible[^1].Remaining > duration - MergeWindowSeconds )
		{
			var last = bucket.Visible[^1];
			last.Count++;
			last.Remaining = Math.Max( last.Remaining, duration );
			bucket.Visible[^1] = last;
			return;
		}

		bucket.Queue.Add( new PopupEntry { Text = text, Color = color, Remaining = duration, Count = 1 } );
		FlushQueue( bucket );
	}

	public static YaUiPopupZone ResolveZone( string text )
	{
		var trimmed = text.Trim();
		if ( trimmed.StartsWith( "+", StringComparison.Ordinal ) )
			return YaUiPopupZone.BottomLeftPersonal;

		return YaUiPopupZone.TopRightCombat;
	}

	public void Tick( float dt )
	{
		foreach ( var bucket in _zones.Values )
		{
			for ( var i = bucket.Visible.Count - 1; i >= 0; i-- )
			{
				var e = bucket.Visible[i];
				e.Remaining -= dt;
				if ( e.Remaining <= 0f )
					bucket.Visible.RemoveAt( i );
				else
					bucket.Visible[i] = e;
			}

			FlushQueue( bucket );
		}
	}

	public (string Text, Color Color)? GetVisibleLine( YaUiPopupZone zone )
	{
		if ( !TryGetVisibleEntry( zone, out var entry ) )
			return null;

		var label = entry.Count > 1 ? $"{entry.Text} x{entry.Count}" : entry.Text;
		return (label, entry.Color);
	}

	public bool AnyVisible()
	{
		foreach ( var zone in Enum.GetValues<YaUiPopupZone>() )
		{
			if ( TryGetVisibleEntry( zone, out _ ) )
				return true;
		}

		return false;
	}

	public void Clear()
	{
		_zones.Clear();
	}

	ZoneQueue GetZone( YaUiPopupZone zone )
	{
		if ( !_zones.TryGetValue( zone, out var bucket ) )
		{
			bucket = new ZoneQueue();
			_zones[zone] = bucket;
		}

		return bucket;
	}

	bool TryGetVisibleEntry( YaUiPopupZone zone, out PopupEntry entry )
	{
		entry = default;
		if ( !_zones.TryGetValue( zone, out var bucket ) || bucket.Visible.Count == 0 )
			return false;

		entry = bucket.Visible[0];
		return true;
	}

	static void FlushQueue( ZoneQueue bucket )
	{
		while ( bucket.Queue.Count > 0 && bucket.Visible.Count < MaxVisiblePerZone )
		{
			bucket.Visible.Add( bucket.Queue[0] );
			bucket.Queue.RemoveAt( 0 );
		}
	}
}
