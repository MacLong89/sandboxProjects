namespace Sandbox;

[Title( "Aimbox Spawn Point" )]
[Category( "Aimbox" )]
public sealed class AimboxSpawnPoint : Component
{
	[Property] public AimboxTeam Team { get; set; }
	[Property] public float Weight { get; set; } = 1f;
}
