using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Sandbox;

/// <summary>Local-only client preferences (onboarding, UI).</summary>
public static class YaClientPrefs
{
	const string PrefsPath = "youarenotalone_client_prefs.json";

	static bool _loaded;
	static bool _hasSeenControlsTutorial;
	static bool _hideTutorialTips;
	static List<string> _tutorialTipsShown = new();
	static List<string> _onboardingGoalsCompleted = new();

	public static bool HasSeenControlsTutorial
	{
		get
		{
			EnsureLoaded();
			return _hasSeenControlsTutorial;
		}
		set
		{
			EnsureLoaded();
			if ( _hasSeenControlsTutorial == value )
				return;
			_hasSeenControlsTutorial = value;
			Persist();
		}
	}

	public static bool HideTutorialTips
	{
		get
		{
			EnsureLoaded();
			return _hideTutorialTips;
		}
		set
		{
			EnsureLoaded();
			if ( _hideTutorialTips == value )
				return;
			_hideTutorialTips = value;
			Persist();
		}
	}

	public static IReadOnlyList<string> TutorialTipsShown
	{
		get
		{
			EnsureLoaded();
			return _tutorialTipsShown;
		}
	}

	public static IReadOnlyList<string> OnboardingGoalsCompleted
	{
		get
		{
			EnsureLoaded();
			return _onboardingGoalsCompleted;
		}
	}

	public static void MarkGoalComplete( string goalId )
	{
		if ( string.IsNullOrEmpty( goalId ) )
			return;

		EnsureLoaded();
		if ( _onboardingGoalsCompleted.Contains( goalId ) )
			return;

		_onboardingGoalsCompleted.Add( goalId );
		Persist();
	}

	public static void MarkTipShown( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
			return;

		EnsureLoaded();
		if ( _tutorialTipsShown.Contains( id ) )
			return;

		_tutorialTipsShown.Add( id );
		Persist();
	}

	static void EnsureLoaded()
	{
		if ( _loaded )
			return;

		_loaded = true;
		try
		{
			if ( !FileSystem.Data.FileExists( PrefsPath ) )
				return;

			using var stream = FileSystem.Data.OpenRead( PrefsPath );
			var doc = JsonDocument.Parse( stream );
			var root = doc.RootElement;

			if ( root.TryGetProperty( "HasSeenControlsTutorial", out var seen ) && seen.ValueKind == JsonValueKind.True )
				_hasSeenControlsTutorial = true;

			if ( root.TryGetProperty( "HideTutorialTips", out var hide ) && hide.ValueKind == JsonValueKind.True )
				_hideTutorialTips = true;

			if ( root.TryGetProperty( "TutorialTipsShown", out var tips ) && tips.ValueKind == JsonValueKind.Array )
			{
				foreach ( var item in tips.EnumerateArray() )
				{
					if ( item.ValueKind == JsonValueKind.String )
					{
						var id = item.GetString();
						if ( !string.IsNullOrEmpty( id ) && !_tutorialTipsShown.Contains( id ) )
							_tutorialTipsShown.Add( id );
					}
				}
			}

			if ( root.TryGetProperty( "OnboardingGoalsCompleted", out var goals ) && goals.ValueKind == JsonValueKind.Array )
			{
				foreach ( var item in goals.EnumerateArray() )
				{
					if ( item.ValueKind == JsonValueKind.String )
					{
						var id = item.GetString();
						if ( !string.IsNullOrEmpty( id ) && !_onboardingGoalsCompleted.Contains( id ) )
							_onboardingGoalsCompleted.Add( id );
					}
				}
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[YA] Failed reading client prefs: {ex.Message}" );
		}
	}

	static void Persist()
	{
		try
		{
			using var stream = FileSystem.Data.OpenWrite( PrefsPath );
			using var writer = new Utf8JsonWriter( stream );
			writer.WriteStartObject();
			writer.WriteBoolean( "HasSeenControlsTutorial", _hasSeenControlsTutorial );
			writer.WriteBoolean( "HideTutorialTips", _hideTutorialTips );
			writer.WriteStartArray( "TutorialTipsShown" );
			foreach ( var id in _tutorialTipsShown )
				writer.WriteStringValue( id );
			writer.WriteEndArray();
			writer.WriteStartArray( "OnboardingGoalsCompleted" );
			foreach ( var id in _onboardingGoalsCompleted )
				writer.WriteStringValue( id );
			writer.WriteEndArray();
			writer.WriteEndObject();
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[YA] Failed writing client prefs: {ex.Message}" );
		}
	}
}
