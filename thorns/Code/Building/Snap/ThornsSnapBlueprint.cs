namespace Sandbox;

/// <summary>Inbound authored socket on an existing placed structure.</summary>
public sealed record ThornsSnapSocketBlueprint(
	int SocketIndex,
	Vector3 LocalPosition,
	Vector3 LocalForwardHorizontal,
	HashSet<ThornsSnapChannel> Accepts );

/// <summary>Outbound mating feature on structure being placed.</summary>
public sealed record ThornsSnapPlugBlueprint(
	int PlugIndex,
	ThornsSnapChannel Channel );
