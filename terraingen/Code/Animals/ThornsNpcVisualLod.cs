namespace Terraingen.Animals;

using Terraingen;

/// <summary>Hides or simplifies distant NPC renderers on all peers (client visual only).</summary>
[Title( "Thorns NPC Visual LOD" )]
[Category( "Thorns/Animals" )]
public sealed class ThornsNpcVisualLod : Component
{
	[Property] public float HideBeyondDistance { get; set; } = ThornsNpcLod.ReducedDistance;
	[Property] public float RefreshSeconds { get; set; } = 0.35f;

	SkinnedModelRenderer _skinned;
	ModelRenderer _model;
	TimeUntil _nextRefresh;
	bool _tierInitialized;
	ThornsNpcLodTier _appliedTier;
	bool _shadowsEnabled = true;

	protected override void OnStart()
	{
		_skinned = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		_model = Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
	}

	protected override void OnUpdate()
	{
		if ( _nextRefresh )
			return;

		_nextRefresh = RefreshSeconds;
		ApplyTier( ResolveLocalTier() );
	}

	ThornsNpcLodTier ResolveLocalTier()
	{
		var scene = Scene;
		if ( scene is null || !scene.IsValid() )
			return ThornsNpcLodTier.Full;

		var observer = ThornsSceneObserver.FindLocalPlayerObject( scene );
		if ( !observer.IsValid() )
			return ThornsNpcLodTier.Full;

		var distSq = (GameObject.WorldPosition.WithZ( 0f ) - observer.WorldPosition.WithZ( 0f )).LengthSquared;
		return ThornsNpcLod.TierForDistanceSquared( distSq );
	}

	void ApplyTier( ThornsNpcLodTier tier )
	{
		if ( _tierInitialized && tier == _appliedTier )
			return;

		_tierInitialized = true;
		_appliedTier = tier;
		// Bandits stay visible at all distances so distant sim + walk-in does not look like a teleport pop-in.
		var visible = tier != ThornsNpcLodTier.Sleeping || GameObject.Tags.Has( "bandit" );

		if ( _skinned.IsValid() )
			_skinned.Enabled = visible;

		if ( _model.IsValid() )
			_model.Enabled = visible;

		var wantShadows = tier == ThornsNpcLodTier.Full;
		if ( _shadowsEnabled == wantShadows )
			return;

		_shadowsEnabled = wantShadows;
		var renderType = wantShadows ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.Off;
		if ( _skinned.IsValid() )
			_skinned.RenderType = renderType;

		if ( _model.IsValid() )
			_model.RenderType = renderType;
	}
}
