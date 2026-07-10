namespace Terraingen.NpcGuild;

using Terraingen;
using Terraingen.Multiplayer;

/// <summary>Claimable outpost core — destroying HQ eliminates the NPC guild.</summary>
public sealed class ThornsNpcGuildCore : Component
{
	public const float InteractRangeInches = 220f;
	public const float ClaimHoldSeconds = 5f;
	public const float EnemyBlockRadiusInches = 1181f;

	[Property] public string GuildId { get; set; } = "";
	[Property] public string OutpostId { get; set; } = "";
	[Property] public bool IsHeadquarters { get; set; }

	public Vector3 CenterWorld => GameObject.IsValid() ? GameObject.WorldPosition : Vector3.Zero;

	public static bool TryPickAlongRay(
		GameObject playerRoot,
		out ThornsNpcGuildCore core,
		out float distance )
	{
		core = null;
		distance = float.MaxValue;
		if ( playerRoot is null || !playerRoot.IsValid() )
			return false;

		if ( !TryResolveAimRay( playerRoot, out var origin, out var forward ) )
			return false;

		var scene = playerRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var end = origin + forward.Normal * InteractRangeInches;
		var trace = scene.Trace
			.Ray( origin, end )
			.WithTag( "npc_guild_core" )
			.IgnoreGameObjectHierarchy( playerRoot )
			.Run();

		if ( trace.Hit && trace.GameObject.IsValid() )
		{
			var hitCore = trace.GameObject.Components.Get<ThornsNpcGuildCore>( FindMode.EverythingInSelfAndParent );
			if ( hitCore.IsValid() && hitCore.Enabled )
			{
				core = hitCore;
				distance = trace.Distance;
				return true;
			}
		}

		return TryPickNearest( origin, forward, InteractRangeInches, out core, out distance );
	}

	static bool TryPickNearest(
		Vector3 origin,
		Vector3 forward,
		float maxRange,
		out ThornsNpcGuildCore core,
		out float distance )
	{
		core = null;
		distance = float.MaxValue;
		var dir = forward.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var candidate in scene.GetAllComponents<ThornsNpcGuildCore>() )
		{
			if ( candidate is null || !candidate.IsValid() || !candidate.Enabled )
				continue;

			var center = candidate.CenterWorld + Vector3.Up * 32f;
			if ( !TryRaySphere( origin, dir, center, 72f, out var dist ) || dist > maxRange || dist >= distance )
				continue;

			distance = dist;
			core = candidate;
		}

		return core is not null;
	}

	static bool TryResolveAimRay( GameObject root, out Vector3 origin, out Vector3 forward )
	{
		if ( ThornsSceneObserver.TryResolveLocalAimRay( root, out origin, out forward ) )
			return true;

		var controller = root.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		origin = root.WorldPosition + Vector3.Up * 64f;
		forward = controller.EyeAngles.ToRotation().Forward.Normal;
		return forward.Length >= 0.95f;
	}

	static bool TryRaySphere( Vector3 origin, Vector3 direction, Vector3 center, float radius, out float distance )
	{
		distance = float.MaxValue;
		var oc = origin - center;
		var b = 2f * Vector3.Dot( oc, direction );
		var c = oc.LengthSquared - radius * radius;
		var discriminant = b * b - 4f * c;
		if ( discriminant < 0f )
			return false;

		var sqrt = MathF.Sqrt( discriminant );
		var t0 = (-b - sqrt) * 0.5f;
		var t1 = (-b + sqrt) * 0.5f;
		distance = t0 >= 0f ? t0 : t1;
		return distance >= 0f;
	}
}
