namespace Terraingen.Player;

using Sandbox;
using Terraingen.Multiplayer;

/// <summary>Local-owner checks and eye resolution for FP viewmodels.</summary>
public static class ThornsLocalPlayer
{
	/// <summary>Networked player root for a component (never the scene root).</summary>
	public static GameObject ResolvePawnRoot( GameObject from )
	{
		if ( !from.IsValid() )
			return default;

		GameObject pawn = default;
		for ( var n = from; n.IsValid(); n = n.Parent )
		{
			if ( IsPlayerPawnRoot( n ) )
				pawn = n;
		}

		return pawn.IsValid() ? pawn : from;
	}

	/// <summary>True for human explorer pawns — excludes wildlife/NPC roots even if they picked up stray player components.</summary>
	public static bool IsPlayerPawnRoot( GameObject root )
	{
		if ( !root.IsValid() )
			return false;

		if ( root.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndParent ) is { IsValid: true } )
			return true;

		return root.Components.Get<ThornsPlayerSession>( FindMode.EverythingInSelfAndParent ) is { IsValid: true };
	}

	public static bool IsLocalConnectionPlayerRoot( GameObject pawnRoot )
	{
		pawnRoot = ResolvePawnRoot( pawnRoot );
		if ( !pawnRoot.IsValid() || !IsPlayerPawnRoot( pawnRoot ) )
			return false;

		if ( !Networking.IsActive )
			return true;

		// Network ownership can lag a few frames after spawn; Local gameplay is bound first.
		var localGameplay = ThornsPlayerGameplay.Local;
		if ( localGameplay.IsValid()
		     && ReferenceEquals( ResolvePawnRoot( localGameplay.GameObject ), pawnRoot ) )
			return true;

		var local = Connection.Local;
		if ( local is null )
			return false;

		if ( pawnRoot.Network.Owner == local )
			return true;

		return pawnRoot.Network.OwnerId == local.Id;
	}

	/// <summary>True when this pawn should receive input, look, and FP presentation on this machine.</summary>
	public static bool IsLocallyControlledPawn( GameObject pawnRoot )
	{
		pawnRoot = ResolvePawnRoot( pawnRoot );
		if ( !pawnRoot.IsValid() || !IsPlayerPawnRoot( pawnRoot ) )
			return false;

		if ( !Networking.IsActive )
			return true;

		return IsLocalConnectionPlayerRoot( pawnRoot );
	}

	public static bool IsLocalConnectionSession( ThornsPlayerSession session )
	{
		if ( session is null || !session.IsValid() )
			return false;

		var local = Connection.Local;
		return local is not null && session.OwnerConnection?.Id == local.Id;
	}

	public static bool IsLocalConnectionOwner( Component component )
	{
		if ( component is null || !component.IsValid() )
			return false;

		return IsLocallyControlledPawn( ResolvePawnRoot( component.GameObject ) );
	}

	public static bool TryGetAuthoritativeEye( GameObject pawnRoot, out Vector3 eyeWorldPos, out Rotation eyeWorldRot )
	{
		eyeWorldPos = default;
		eyeWorldRot = Rotation.Identity;

		pawnRoot = ResolvePawnRoot( pawnRoot );
		if ( !pawnRoot.IsValid() )
			return false;

		var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( pawnRoot );
		if ( rig.IsValid() )
		{
			eyeWorldPos = rig.WorldPosition;
			eyeWorldRot = rig.WorldRotation;
			return true;
		}

		var controller = pawnRoot.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( controller.IsValid() )
		{
			var pitchQ = Rotation.FromAxis( Vector3.Right, controller.EyeAngles.pitch );
			var eyeLocal = new Vector3( 0f, 0f, ThornsPlayerFirstPersonRig.DefaultEyeOffsetZ );
			eyeWorldPos = pawnRoot.WorldPosition + pawnRoot.WorldRotation * eyeLocal;
			eyeWorldRot = pawnRoot.WorldRotation * pitchQ;
			return true;
		}

		return false;
	}
}
