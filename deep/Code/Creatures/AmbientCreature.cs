namespace Deep;

/// <summary>Non-hostile fauna that fills the water column and feeds the journal.</summary>
public sealed class AmbientCreature : Component
{
	public CreatureDefinition Definition { get; private set; }
	public float DiscoverRadius { get; set; } = 6.5f;

	private bool _discoveredThisDive;

	public void Setup( CreatureDefinition def )
	{
		Definition = def;
		if ( def is null ) return;

		var tex = !string.IsNullOrEmpty( def.TexturePath )
			? DeepPixelArt.Load( def.TexturePath )
			: Texture.White;
		if ( tex.IsValid() && tex != Texture.White )
			DeepSprites.SpawnTexture( GameObject, tex, def.SpriteWorldHeight, name: "Sprite" );
	}

	protected override void OnUpdate()
	{
		if ( _discoveredThisDive || Definition is null )
			return;

		var game = DeepGame.Instance;
		if ( game is null || !game.State.IsDivingActive )
			return;

		var diver = game.Diver;
		if ( diver is null ) return;

		if ( (diver.WorldPosition - WorldPosition).Length > DiscoverRadius )
			return;

		_discoveredThisDive = true;
		if ( game.Progression.RegisterCreature( Definition.Id ) )
		{
			game.ShowMessage( $"Journal: {Definition.DisplayName}", 1.4f );
			game.DiveLog?.AddEntry( Definition.DisplayName, IconPath(), known: true );
		}
	}

	private string IconPath()
	{
		if ( string.IsNullOrEmpty( Definition?.TexturePath ) )
			return "/ui/icons/map_loot.png";
		return "/" + Definition.TexturePath.Replace( '\\', '/' );
	}
}
