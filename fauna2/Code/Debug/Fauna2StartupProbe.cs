namespace Fauna2;



/// <summary>Attach to scene Systems object — logs boot diagnostics to console.</summary>

public sealed class Fauna2StartupProbe : Component

{

	private bool _loggedStart;

	private bool _ranDelayedProbe;

	private TimeUntil _delayedProbe;



	protected override void OnAwake()

	{

		Fauna2Debug.Info( "Boot", $"OnAwake scene='{Scene?.Name}' editor={Scene.IsEditor}" );

		Fauna2Debug.LogNetworking( "OnAwake" );

	}



	protected override void OnStart()

	{

		DefinitionCatalog.EnsureInitialized();



		if ( _loggedStart ) return;

		_loggedStart = true;



		Fauna2Debug.Info( "Boot", "OnStart — running full probe" );

		Fauna2Debug.LogSceneProbe( Scene, "OnStart" );

		Fauna2Debug.LogNetworking( "OnStart" );

		Fauna2Debug.LogDefinitions();

		Fauna2Debug.LogAssets();

		Fauna2Debug.LogSystems( "OnStart" );



		_delayedProbe = 1f;

	}



	protected override void OnUpdate()

	{

		if ( !Fauna2Debug.Enabled ) return;



		Fauna2Debug.TickHeartbeat();



		if ( _ranDelayedProbe || _delayedProbe )

			return;



		_ranDelayedProbe = true;

		Fauna2Debug.LogSceneProbe( Scene, "T+1s" );

		Fauna2Debug.LogSystems( "T+1s" );

	}

}

