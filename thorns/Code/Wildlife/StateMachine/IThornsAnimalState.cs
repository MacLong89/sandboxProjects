namespace Sandbox;

/// <summary>Single behavior state in the animal AI machine.</summary>
public interface IThornsAnimalState
{
	ThornsWildlifeAiState StateId { get; }

	void OnEnter( ThornsAnimalBrainContext ctx );
	void OnExit( ThornsAnimalBrainContext ctx );

	/// <summary>Periodic decision-making — not every frame.</summary>
	void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat );

	/// <summary>Every physics step — movement wish only.</summary>
	void SyncMotorWish( ThornsAnimalBrainContext ctx, ThornsWildlifeMotor motor, Vector3 selfFlat );
}
