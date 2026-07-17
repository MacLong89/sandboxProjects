namespace DeepDive.UI;

/// <summary>Light UI click feedback — fails silently if assets are missing.</summary>
public static class UiSfx
{
	private static readonly HashSet<string> _missing = new();

	public static void Click( float pitch = 1f )
	{
		TryPlay( "sounds/ui_click.sound", 0.55f, pitch );
	}

	public static void Purchase( float pitch = 1.05f )
	{
		TryPlay( "sounds/ui_purchase.sound", 0.7f, pitch );
	}

	public static void Deny( float pitch = 0.85f )
	{
		TryPlay( "sounds/ui_deny.sound", 0.45f, pitch );
	}

	private static void TryPlay( string path, float volume, float pitch )
	{
		if ( string.IsNullOrEmpty( path ) || _missing.Contains( path ) )
			return;

		try
		{
			var handle = Sound.Play( path );
			if ( handle is null )
			{
				_missing.Add( path );
				return;
			}

			handle.Volume = volume;
			if ( MathF.Abs( pitch - 1f ) > 0.001f )
				handle.Pitch = pitch;
		}
		catch
		{
			_missing.Add( path );
		}
	}
}
