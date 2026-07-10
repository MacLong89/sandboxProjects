namespace Terraingen.Clutter;

using Sandbox;

/// <summary>Soft alpha fade-in for newly streamed mesh clutter instances.</summary>
sealed class ThornsClutterReveal
{
	struct Entry
	{
		public ModelRenderer Renderer;
		public Color TargetTint;
		public double StartTime;
		public float Duration;
	}

	readonly List<Entry> _entries = new();

	public int ActiveCount => _entries.Count;

	public void Reset() => _entries.Clear();

	public void Queue( ModelRenderer renderer, Color targetTint, float delaySeconds, float durationSeconds )
	{
		if ( renderer is null || !renderer.IsValid() || durationSeconds <= 0f )
			return;

		RemoveFor( renderer );
		renderer.Tint = targetTint.WithAlpha( 0f );
		_entries.Add( new Entry
		{
			Renderer = renderer,
			TargetTint = targetTint,
			StartTime = Time.Now + Math.Max( 0f, delaySeconds ),
			Duration = durationSeconds,
		} );
	}

	public void RemoveFor( ModelRenderer renderer )
	{
		if ( renderer is null || !renderer.IsValid() )
			return;

		for ( var i = _entries.Count - 1; i >= 0; i-- )
		{
			if ( _entries[i].Renderer == renderer )
				_entries.RemoveAt( i );
		}
	}

	public void RemoveForObject( GameObject instance )
	{
		if ( instance is null || !instance.IsValid() )
			return;

		var renderer = instance.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		if ( renderer is not null && renderer.IsValid() )
			RemoveFor( renderer );
	}

	public void Tick()
	{
		if ( _entries.Count == 0 )
			return;

		var now = Time.Now;
		for ( var i = _entries.Count - 1; i >= 0; i-- )
		{
			var entry = _entries[i];
			if ( entry.Renderer is null || !entry.Renderer.IsValid() )
			{
				_entries.RemoveAt( i );
				continue;
			}

			var elapsed = (float)(now - entry.StartTime);
			if ( elapsed <= 0f )
				continue;

			var t = (elapsed / entry.Duration).Clamp( 0f, 1f );
			var eased = t * t * (3f - 2f * t);
			entry.Renderer.Tint = entry.TargetTint.WithAlpha( eased );
			if ( t >= 1f )
				_entries.RemoveAt( i );
		}
	}
}
