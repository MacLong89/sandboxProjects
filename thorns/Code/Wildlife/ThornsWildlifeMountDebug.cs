namespace Sandbox;

/// <summary>
/// Mount flow tracing — set <see cref="Enabled"/> false to silence. Filter console by <c>[Thorns][Mount]</c>.
/// </summary>
public static class ThornsWildlifeMountDebug
{
	public static bool Enabled = true;

	public static void Write( string message )
	{
		if ( !Enabled )
			return;
		Log.Info( $"[Thorns][Mount] {message}" );
	}
}
