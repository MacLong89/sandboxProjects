namespace Sandbox;

public enum ThornsTerrainSeedKind
{
	NotTerrain,
	SlabOnRay
}

/// <summary>
/// Client preview + RPC payload — host reruns the same snap recipe with <see cref="ThornsPlacedStructure"/> truth.
/// </summary>
public sealed class ThornsPlacementSuggestion
{
	public bool UsesSocketSnap;
	public ThornsPlacementSocketBind HostSnap;
	public ThornsSnapChannel Channel;
	public ushort IncomingPlugIndex;
	public ushort OppositeTwinSocketPreview;
	public Vector3 ProposedWorldPosition;
	public Rotation ProposedWorldRotation;
	public ThornsTerrainSeedKind TerrainKind;

	public ThornsPlacementSuggestion Clone() =>
		new()
		{
			UsesSocketSnap = UsesSocketSnap,
			HostSnap = HostSnap,
			Channel = Channel,
			IncomingPlugIndex = IncomingPlugIndex,
			OppositeTwinSocketPreview = OppositeTwinSocketPreview,
			ProposedWorldPosition = ProposedWorldPosition,
			ProposedWorldRotation = ProposedWorldRotation,
			TerrainKind = TerrainKind
		};
}
