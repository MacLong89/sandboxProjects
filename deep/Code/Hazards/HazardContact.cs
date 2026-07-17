namespace Deep;

public enum HazardKind
{
	Mine,
	Jellyfish,
	Puffer,
	Angler
}

public sealed class HazardContact : Component
{
	public HazardKind Kind { get; set; }
	public float Damage { get; set; } = 22f;
	public float HitRadius { get; set; } = 2.2f;

	private TimeUntil _cooldownReady;

	public void Setup( HazardKind kind, float damage, float spriteHeight, float hitRadius = 2.2f )
	{
		Kind = kind;
		Damage = damage;
		HitRadius = hitRadius;

		var tex = kind switch
		{
			HazardKind.Mine => DeepPixelArt.Mine(),
			HazardKind.Jellyfish => DeepPixelArt.Jellyfish(),
			HazardKind.Puffer => DeepPixelArt.Puffer(),
			HazardKind.Angler => DeepPixelArt.Angler(),
			_ => Texture.White
		};

		if ( tex is not null && tex.IsValid() && tex != Texture.White )
		{
			DeepSprites.SpawnTexture( GameObject, tex, spriteHeight, name: "Sprite" );
			return;
		}

		// Fallback box if art missing.
		var go = new GameObject( GameObject, true, "Visual" );
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( spriteHeight * 0.5f ) );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = kind switch
		{
			HazardKind.Jellyfish => new Color( 0.72f, 0.35f, 0.9f ),
			HazardKind.Puffer => new Color( 0.95f, 0.8f, 0.25f ),
			HazardKind.Angler => new Color( 0.35f, 0.2f, 0.15f ),
			_ => new Color( 0.25f, 0.25f, 0.28f )
		};
	}

	protected override void OnUpdate()
	{
		var game = DeepGame.Instance;
		if ( game is null || !game.State.IsDivingActive )
			return;

		var diver = game.Diver;
		var health = game.Health;
		if ( diver is null || health is null )
			return;

		if ( (diver.WorldPosition - WorldPosition).Length > HitRadius )
			return;

		// Bio lure distracts soft wildlife.
		if ( game.Tools?.LureActive == true && Kind is HazardKind.Jellyfish or HazardKind.Puffer )
			return;

		if ( !_cooldownReady )
			return;

		_cooldownReady = game.Balance.HazardCooldownSeconds;
		health.ApplyDamage( Damage, Kind.ToString() );
		game.Run.AddDamage( Damage );
		var creature = CreatureCatalog.FromHazard( Kind );
		if ( creature is not null )
			game.Progression.RegisterCreature( creature.Id );
		game.ShowMessage( HitMessage(), 1.1f );
	}

	private string HitMessage() => Kind switch
	{
		HazardKind.Mine => "Mine blast!",
		HazardKind.Jellyfish => "Jellyfish sting!",
		HazardKind.Puffer => "Puffer spines!",
		HazardKind.Angler => "Angler bite!",
		_ => "Ouch!"
	};
}
