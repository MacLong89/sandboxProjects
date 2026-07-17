namespace Deep;

public sealed class CollectiblePickup : Component
{
	public CollectibleDefinition Definition { get; set; }
	public bool Collected { get; private set; }
	public bool Revealed { get; private set; }
	public string ScanKey { get; private set; }

	private TimeUntil _bagFullToastReady;
	private ModelRenderer _fallbackRenderer;
	private SpriteRenderer _sprite;

	public void Setup( CollectibleDefinition def )
	{
		Definition = def;
		ScanKey = $"{def?.Id}_{Guid.NewGuid():N}";
		Revealed = def is null || !def.RequiresScan;
		BuildVisual();
		ApplyRevealVisual();
	}

	public void Reveal()
	{
		if ( Revealed || Definition is null ) return;
		Revealed = true;
		ApplyRevealVisual();
	}

	protected override void OnUpdate()
	{
		if ( Collected || Definition is null )
			return;

		var game = DeepGame.Instance;
		if ( game is null || !game.State.IsDivingActive )
			return;

		var diver = game.Diver;
		if ( diver is null )
			return;

		var radius = game.Balance.CollectPickupRadius;
		if ( (diver.WorldPosition - WorldPosition).Length > radius )
			return;

		TryCollect( game );
	}

	private void TryCollect( DeepGame game )
	{
		if ( Collected || Definition is null )
			return;

		if ( Definition.RequiresScan && !Revealed )
		{
			game.ShowMessage( "Scan to reveal", 1.0f );
			return;
		}

		if ( Definition.RequiredTool is ToolKind need )
		{
			if ( game.Tools?.Hotbar.SelectedTool?.Kind != need )
			{
				game.ShowMessage( $"Equip {need} to salvage", 1.1f );
				return;
			}
		}

		if ( !game.Run.Haul.CanFit( Definition ) )
		{
			if ( _bagFullToastReady )
			{
				_bagFullToastReady = 1.25f;
				game.ShowMessage( "Bag full!", 1.2f );
			}
			return;
		}

		if ( !game.Run.Haul.TryAdd( Definition ) )
			return;

		Collected = true;
		game.ShowMessage( $"+ {Definition.DisplayName} (${Definition.BaseValue:0})", 1.4f );
		game.Progression.RegisterDiscovery( Definition.Id );
		var icon = string.IsNullOrEmpty( Definition.TexturePath )
			? "/ui/icons/map_loot.png"
			: "/" + Definition.TexturePath.Replace( '\\', '/' );
		game.DiveLog?.AddEntry( Definition.DisplayName, icon, known: true );
		GameObject.Destroy();
	}

	private void BuildVisual()
	{
		if ( Definition is null ) return;

		Texture tex = null;
		if ( !string.IsNullOrEmpty( Definition.TexturePath ) )
			tex = DeepPixelArt.Load( Definition.TexturePath );

		if ( tex is not null && tex.IsValid() && tex != Texture.White )
		{
			_sprite = DeepSprites.SpawnTexture( GameObject, tex, Definition.SpriteWorldHeight, name: "Sprite" );
			return;
		}

		var go = new GameObject( GameObject, true, "Visual" );
		go.LocalScale = MeshPrimitives.BoxScale( Definition.WorldSize );
		_fallbackRenderer = go.Components.Create<ModelRenderer>();
		_fallbackRenderer.Model = MeshPrimitives.Box;
		_fallbackRenderer.MaterialOverride = MeshPrimitives.Mat;
		_fallbackRenderer.Tint = Definition.Tint;
	}

	private void ApplyRevealVisual()
	{
		if ( Definition is null ) return;
		if ( Revealed )
		{
			if ( _fallbackRenderer.IsValid() )
				_fallbackRenderer.Tint = Definition.Tint;
			return;
		}

		// Locked / unscanned — dim silhouette.
		if ( _fallbackRenderer.IsValid() )
			_fallbackRenderer.Tint = new Color( 0.15f, 0.18f, 0.22f );
	}
}
