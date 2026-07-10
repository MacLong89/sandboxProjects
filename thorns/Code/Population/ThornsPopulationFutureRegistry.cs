namespace Sandbox;

/// <summary>Future population channels — interfaces and stubs only (breeding, tames, mounts, guild NPCs, traders, bosses).</summary>
public interface IThornsPopulationEntity
{
	ThornsPopulationKind PopulationKind { get; }
	GameObject PopulationRoot { get; }
}

public interface IThornsTamePopulationEntity : IThornsPopulationEntity
{
	Guid TameId { get; }
	string OwnerAccountKey { get; }
}

public interface IThornsMountPopulationEntity : IThornsTamePopulationEntity
{
	GameObject MountedRider { get; }
}

public interface IThornsBreedingPopulationEntity : IThornsTamePopulationEntity
{
	Guid? MateId { get; }
}

public interface IThornsGuildNpcPopulationEntity : IThornsPopulationEntity
{
	string GuildId { get; }
}

public interface IThornsTraderPopulationEntity : IThornsPopulationEntity
{
	string TraderDefId { get; }
}

public interface IThornsBossPopulationEntity : IThornsPopulationEntity
{
	string BossDefId { get; }
}

/// <summary>Reserved registry slots — wired when gameplay systems land.</summary>
public static class ThornsPopulationFutureRegistry
{
	public static int HostTameCount => 0;
	public static int HostMountRiderCount => 0;
	public static int HostBreedingPairCount => 0;
	public static int HostGuildNpcCount => 0;
	public static int HostTraderCount => 0;
	public static int HostBossCount => 0;

	public static void HostRegisterTame( IThornsTamePopulationEntity entity ) => _ = entity;
	public static void HostUnregisterTame( IThornsTamePopulationEntity entity ) => _ = entity;
	public static void HostRegisterMount( IThornsMountPopulationEntity entity ) => _ = entity;
	public static void HostUnregisterMount( IThornsMountPopulationEntity entity ) => _ = entity;
	public static void HostRegisterBreedingPair( IThornsBreedingPopulationEntity entity ) => _ = entity;
	public static void HostUnregisterBreedingPair( IThornsBreedingPopulationEntity entity ) => _ = entity;
	public static void HostRegisterGuildNpc( IThornsGuildNpcPopulationEntity entity ) => _ = entity;
	public static void HostUnregisterGuildNpc( IThornsGuildNpcPopulationEntity entity ) => _ = entity;
	public static void HostRegisterTrader( IThornsTraderPopulationEntity entity ) => _ = entity;
	public static void HostUnregisterTrader( IThornsTraderPopulationEntity entity ) => _ = entity;
	public static void HostRegisterBoss( IThornsBossPopulationEntity entity ) => _ = entity;
	public static void HostUnregisterBoss( IThornsBossPopulationEntity entity ) => _ = entity;
}
