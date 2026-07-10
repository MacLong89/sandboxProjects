namespace Terraingen.Animals;

/// <summary>Client-side animation from replicated AI state only.</summary>
[Title( "Thorns Animal Visual" )]
[Category( "Thorns/Animals" )]
public sealed class ThornsAnimalVisual : Component
{
	[Property] public SkinnedModelRenderer Renderer { get; set; }

	ThornsAnimalBrain _brain;
	string _displaySequence = "";
	uint _lastStrikeSerial;

	const float MountedRunSpeedThreshold = 16f;
	const float WanderWalkSpeedThreshold = 8f;

	protected override void OnStart()
	{
		_brain = Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		Renderer ??= Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndChildren );
		if ( Renderer.IsValid() )
			ThornsAnimalCameraGuard.ConfigureRenderer( Renderer );

		ThornsAnimalCameraGuard.SuppressStrayCameras( GameObject );
		_ = Components.Get<ThornsAnimalCameraGuardHost>() ?? Components.Create<ThornsAnimalCameraGuardHost>();
	}

	protected override void OnUpdate()
	{
		if ( !_brain.IsValid() || !Renderer.IsValid() )
			return;

		if ( !ThornsAnimalSpeciesRegistry.TryGet( _brain.SpeciesId, out var species ) )
			return;

		var animPrefix = ThornsAnimalModelResolve.ResolveForBrain( _brain ).AnimPrefix;
		if ( string.IsNullOrWhiteSpace( animPrefix ) )
			animPrefix = species.AnimPrefix;

		var brainSequence = ResolveSequence(
			animPrefix,
			_brain.AiState,
			_brain.IsTamedFollowSprinting,
			_brain.ReplicatedMoveSpeed );

		var replayStrike = _brain.AiState == ThornsAnimalState.Attack
		                   && _brain.StrikeSerial != _lastStrikeSerial;
		if ( replayStrike )
			_lastStrikeSerial = _brain.StrikeSerial;

		if ( brainSequence != _displaySequence || replayStrike )
			ApplyDisplaySequence( brainSequence, _brain.AiState, replayStrike );

		if ( IsAttackSequence( _displaySequence ) )
			Renderer.PlaybackRate = Math.Clamp( _brain.StrikeAnimPlaybackRate, 0.5f, 1.5f );
		else
			Renderer.PlaybackRate = 1f;
	}

	void ApplyDisplaySequence( string sequence, ThornsAnimalState brainState, bool restart )
	{
		_displaySequence = sequence;

		if ( restart && Renderer.Sequence.Name == sequence )
			Renderer.Sequence.Name = "";

		Renderer.Sequence.Name = sequence;
		Renderer.Sequence.Looping = brainState != ThornsAnimalState.Dead && IsLocomotionSequence( sequence );
	}

	static bool IsLocomotionSequence( string sequence ) =>
		!string.IsNullOrEmpty( sequence )
		&& (sequence.EndsWith( "_run", StringComparison.OrdinalIgnoreCase )
		    || sequence.EndsWith( "_walk", StringComparison.OrdinalIgnoreCase ));

	static bool IsAttackSequence( string sequence ) =>
		!string.IsNullOrEmpty( sequence ) && sequence.EndsWith( "_attack", StringComparison.OrdinalIgnoreCase );

	static string ResolveSequence(
		string prefix,
		ThornsAnimalState state,
		bool followSprinting,
		float replicatedMoveSpeed )
	{
		return state switch
		{
			ThornsAnimalState.Idle => $"{prefix}_idle",
			ThornsAnimalState.Wander when followSprinting && replicatedMoveSpeed > MountedRunSpeedThreshold => $"{prefix}_run",
			ThornsAnimalState.Wander when replicatedMoveSpeed > WanderWalkSpeedThreshold => $"{prefix}_walk",
			ThornsAnimalState.Wander => $"{prefix}_idle",
			ThornsAnimalState.Chase => $"{prefix}_run",
			ThornsAnimalState.Flee => $"{prefix}_run",
			ThornsAnimalState.Attack => $"{prefix}_attack",
			ThornsAnimalState.Mounted when replicatedMoveSpeed > MountedRunSpeedThreshold => $"{prefix}_run",
			ThornsAnimalState.Mounted => $"{prefix}_idle",
			ThornsAnimalState.Dead => $"{prefix}_death",
			_ => $"{prefix}_idle",
		};
	}
}
