using System;

namespace Sandbox;

public readonly struct ThornsHotTipDefinition
{
	public string Id { get; init; }
	public string Message { get; init; }
	public ThornsHotTipCategory Category { get; init; }
	public int Priority { get; init; }
	public float DurationSeconds { get; init; }
	public bool Repeatable { get; init; }
	public float PerTipCooldownSeconds { get; init; }
	public float MinLookSeconds { get; init; }
	public bool NewPlayerEnhanced { get; init; }

	public ThornsHotTipDefinition(
		string id,
		string message,
		ThornsHotTipCategory category,
		int priority,
		float durationSeconds = 4.8f,
		bool repeatable = false,
		float perTipCooldownSeconds = 90f,
		float minLookSeconds = 0f,
		bool newPlayerEnhanced = false )
	{
		Id = id;
		Message = message;
		Category = category;
		Priority = priority;
		DurationSeconds = durationSeconds;
		Repeatable = repeatable;
		PerTipCooldownSeconds = perTipCooldownSeconds;
		MinLookSeconds = minLookSeconds;
		NewPlayerEnhanced = newPlayerEnhanced;
	}
}

public delegate bool ThornsHotTipCondition( ThornsHotTipContext ctx );

public readonly record struct ThornsHotTipRule(
	ThornsHotTipDefinition Definition,
	ThornsHotTipCondition Condition );
