namespace Sandbox;

/// <summary>Marker + renderer handle for decorative foliage distance culling.</summary>
public sealed class ThornsFoliageCullProxy : Component
{
	[Property] public ModelRenderer TargetRenderer { get; set; }

	/// <summary>When &gt; 0, overrides <see cref="ThornsFoliageDistanceCullSystem.HideDistance"/> for this prop.</summary>
	[Property] public float HideDistanceOverride { get; set; }

	/// <summary>When &gt; 0, overrides <see cref="ThornsFoliageDistanceCullSystem.ShowDistance"/> for this prop.</summary>
	[Property] public float ShowDistanceOverride { get; set; }

	/// <summary>If true, applies one distance sample on start so new chunks do not draw the whole map until the culler catches up.</summary>
	[Property] public bool ApplyDistanceCullOnStart { get; set; }

	public bool IsVisible { get; private set; } = true;

	protected override void OnStart()
	{
		if ( !TargetRenderer.IsValid() )
			TargetRenderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		ApplyVisibleState();

		if ( Game.IsPlaying )
		{
			ThornsFoliageDistanceCullSystem.RegisterProxy( this );
			if ( ApplyDistanceCullOnStart )
				TryApplyStartupDistanceCull();
		}
	}

	protected override void OnDestroy()
	{
		ThornsFoliageDistanceCullSystem.UnregisterProxy( this );
		base.OnDestroy();
	}

	void TryApplyStartupDistanceCull()
	{
		if ( !GameObject.IsValid() || !TargetRenderer.IsValid() )
			return;

		if ( !ThornsFoliageDistanceCullSystem.TryGetCachedViewerPosition( out var viewerPos ) )
			return;

		var hide = HideDistanceOverride > 0f
			? HideDistanceOverride
			: ThornsFoliageDistanceCullSystem.DefaultHideDistance;

		var d2 = (GameObject.WorldPosition - viewerPos).LengthSquared;
		var hideSq = hide * hide;
		// First sample: beyond hide radius → off immediately (hysteresis handled on next culler ticks).
		SetVisible( d2 <= hideSq );
	}

	public void SetVisible( bool visible )
	{
		if ( IsVisible == visible )
			return;

		IsVisible = visible;
		ApplyVisibleState();
	}

	void ApplyVisibleState()
	{
		if ( TargetRenderer.IsValid() )
			TargetRenderer.Enabled = IsVisible;
	}
}

