namespace Fauna2;

/// <summary>
/// A rare visual/genetic variant (albino, melanistic, golden, arctic...).
/// Variants are rolled when animals are acquired and — more often — bred.
/// They multiply guest appeal and animal value.
/// </summary>
[AssetType( Name = "Fauna Variant", Extension = "variant", Category = "Fauna" )]
public sealed class VariantDefinition : GameResource
{
	[Property] public string DisplayName { get; set; } = "Variant";
	[Property, TextArea] public string Description { get; set; } = "";

	/// <summary>Relative weight when a variant roll succeeds. Higher = more common.</summary>
	[Property] public float RarityWeight { get; set; } = 25f;

	[Property] public float AppealMultiplier { get; set; } = 2.5f;
	[Property] public float ValueMultiplier { get; set; } = 3f;

	/// <summary>Overrides the species body tint.</summary>
	[Property] public Color Tint { get; set; } = Color.White;

	/// <summary>
	/// Species (resource names) this variant can appear on. Empty = any species.
	/// </summary>
	[Property] public List<string> ApplicableSpecies { get; set; } = new();

	public bool AppliesTo( AnimalDefinition def )
	{
		if ( def is null ) return false;
		return ApplicableSpecies is null || ApplicableSpecies.Count == 0
			|| ApplicableSpecies.Contains( def.ResourceName );
	}
}
