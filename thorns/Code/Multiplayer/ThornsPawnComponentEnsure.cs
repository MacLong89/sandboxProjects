namespace Sandbox;

/// <summary>Shared helpers for attaching gameplay components to the networked pawn root.</summary>
static class ThornsPawnComponentEnsure
{
	public static bool TryGetPawnGameObject( GameObject root, out GameObject pawnGo )
	{
		pawnGo = default;
		if ( !root.IsValid() )
			return false;

		var pawn = root.Components.GetInDescendantsOrSelf<ThornsPawn>( true );
		if ( !pawn.IsValid() )
			return false;

		pawnGo = pawn.GameObject;
		return pawnGo.IsValid();
	}

	public static void Ensure<T>( GameObject root ) where T : Component, new()
	{
		if ( !TryGetPawnGameObject( root, out var go ) )
			return;

		if ( !go.Components.Get<T>( FindMode.EnabledInSelf ).IsValid() )
			_ = go.Components.Create<T>();
	}
}
