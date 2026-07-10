namespace Terraingen.Player;

using System.Collections.Generic;
using Sandbox;

/// <summary>Throttled FP / viewmodel diagnostics for terraingen (enable while tuning presentation).</summary>
public static class ThornsFpDebug
{
	/// <summary>When true, logs <c>[Thorns][FP]</c> and enables <see cref="Sandbox.ThornsWeaponResourceLoad.FpViewmodelDiagnosticLogs"/>.</summary>
	public static bool Verbose { get; set; }

	static string _lastMsg = "";
	static double _nextLogTime;
	static readonly HashSet<string> _onceKeys = new();

	public static void ApplyToWeaponResourceLoad()
	{
		if ( Verbose )
			ThornsWeaponResourceLoad.FpViewmodelDiagnosticLogs = true;
	}

	public static void Write( string message )
	{
		if ( !Verbose )
			return;

		ApplyToWeaponResourceLoad();

		var now = Time.Now;
		if ( message == _lastMsg && now < _nextLogTime )
			return;

		_lastMsg = message;
		_nextLogTime = now + 0.6;
		Log.Info( "[Thorns][FP] " + message );
	}

	public static void WriteOnce( string key, string message )
	{
		if ( !Verbose || string.IsNullOrEmpty( key ) )
			return;

		if ( !_onceKeys.Add( key ) )
			return;

		ApplyToWeaponResourceLoad();
		Log.Info( "[Thorns][FP] " + message );
	}
}
