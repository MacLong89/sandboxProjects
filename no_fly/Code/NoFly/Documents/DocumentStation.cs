namespace NoFly;

/// <summary>
/// Network-replicated snapshot of the active document inspection.
/// </summary>
public sealed class DocumentStation : Component
{
	[Sync( SyncFlags.FromHost )] public string CurrentPassengerId { get; set; }
	[Sync( SyncFlags.FromHost )] public int QueueCount { get; set; }
	[Sync( SyncFlags.FromHost )] public bool Busy { get; set; }
	[Sync( SyncFlags.FromHost )] public string SubmittedJson { get; set; }
	[Sync( SyncFlags.FromHost )] public string ReferenceJson { get; set; }
	[Sync( SyncFlags.FromHost )] public string SelectedFieldName { get; set; }

	public DocumentInstance ActiveSubmitted => DocNet.FromJson( SubmittedJson );
	public DocumentInstance ActiveReference => DocNet.FromJson( ReferenceJson );
	public DocumentFieldType? SelectedField
	{
		get => Enum.TryParse<DocumentFieldType>( SelectedFieldName, out var f ) ? f : null;
		set => SelectedFieldName = value?.ToString();
	}

	TimeSince _busyTimer;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || !Busy ) return;
		// Unstick if the passenger vanished mid-inspection.
		if ( NoFlyGame.Instance?.FindPlayer( CurrentPassengerId ) is null && _busyTimer > 1.5f )
			Finish();
	}

	public bool BeginInspection( NoFlyPlayer passenger )
	{
		if ( !Networking.IsHost || passenger?.Document is null || Busy ) return false;
		Busy = true;
		_busyTimer = 0;
		CurrentPassengerId = passenger.PlayerId;
		passenger.FlowState = PassengerFlowState.DocumentInspection;

		var submitted = DocNet.Clone( passenger.Document );
		var reference = DocumentCatalog.CreateValid( "ref", passenger.Document.TemplateId );
		reference.Values[DocumentFieldType.Name] = passenger.Document.Values[DocumentFieldType.Name];
		reference.Values[DocumentFieldType.Destination] = passenger.Document.Values.GetValueOrDefault( DocumentFieldType.Destination );
		var template = DocumentCatalog.GetTemplate( passenger.Document.TemplateId );
		reference.Values[DocumentFieldType.Photo] = template.DefaultPhoto;
		reference.Values[DocumentFieldType.Date] = template.DefaultDate;
		reference.Values[DocumentFieldType.PassportNumber] = template.DefaultNumber;
		reference.Values[DocumentFieldType.CountrySymbol] = template.DefaultSymbol;
		reference.Values[DocumentFieldType.SecuritySeal] = template.DefaultSeal;
		reference.Values[DocumentFieldType.BackgroundPattern] = template.DefaultPattern;

		SubmittedJson = DocNet.ToJson( submitted );
		ReferenceJson = DocNet.ToJson( reference );
		SelectedFieldName = null;
		return true;
	}

	public void Approve( NoFlyPlayer agent )
	{
		if ( !Networking.IsHost ) return;
		var passenger = NoFlyGame.Instance?.FindPlayer( CurrentPassengerId );
		if ( passenger is null )
		{
			Finish();
			return;
		}

		QueueSystem.Leave( passenger );

		var forged = passenger.Document?.IsForged == true;
		if ( forged )
		{
			agent?.AddScore( -40, "inspection" );
			passenger.AddScore( 80, "deception" );
		}
		else
		{
			agent?.AddScore( 25, "inspection" );
		}

		passenger.DocumentApproved = true;
		passenger.FlowState = PassengerFlowState.GoingToScanner;
		QueueSystem.JoinScanner( passenger );
		if ( agent is not null )
			agent.ActivePrompt = forged
				? $"Approved — missed a forgery on {passenger.DisplayName}"
				: $"Approved — {passenger.DisplayName} cleared";
		Finish();
	}

	public void Reject( NoFlyPlayer agent, DocumentFieldType? field )
	{
		if ( !Networking.IsHost ) return;
		var game = NoFlyGame.Instance;
		var passenger = game?.FindPlayer( CurrentPassengerId );
		if ( passenger is null || game is null )
		{
			Finish();
			return;
		}
		if ( field is null )
		{
			if ( agent is not null ) agent.ActivePrompt = "Select the suspicious field before rejecting.";
			return;
		}

		QueueSystem.Leave( passenger );

		var forged = passenger.Document?.IsForged == true;
		var correctField = forged && passenger.Document.ForgedField == field;
		if ( correctField )
		{
			agent?.AddScore( 100, "inspection" );
			SecurityCall.Detain( game, passenger, agent, AlertType.DocumentFlag,
				$"Document reject: {passenger.DisplayName} — forged {field}" );
			if ( agent is not null )
				agent.ActivePrompt = $"Rejected {UiLabels.Field( field.Value )} — {passenger.DisplayName} detained";
		}
		else
		{
			// Wrong field (or clean doc) — passenger still leaves the desk so you aren't stuck.
			agent?.AddScore( -50, "inspection" );
			passenger.AddScore( forged ? 40 : -20, forged ? "deception" : "boarding" );
			passenger.DocumentApproved = true;
			passenger.FlowState = PassengerFlowState.GoingToScanner;
			QueueSystem.JoinScanner( passenger );
			if ( agent is not null )
				agent.ActivePrompt = forged
					? $"Wrong field — {passenger.DisplayName} slipped through"
					: $"Wrongful reject — {passenger.DisplayName} cleared anyway";
		}

		Finish();
	}

	public void CallSecurity( NoFlyPlayer agent )
	{
		if ( !Networking.IsHost ) return;
		var game = NoFlyGame.Instance;
		var passenger = game?.FindPlayer( CurrentPassengerId );
		if ( passenger is null || game is null )
		{
			Finish();
			return;
		}

		QueueSystem.Leave( passenger );

		var guilty = passenger.Document?.IsForged == true;
		if ( guilty )
			agent?.AddScore( 70, "inspection" );
		else
			agent?.AddScore( -35, "inspection" );

		SecurityCall.Detain( game, passenger, agent, AlertType.DocumentFlag,
			guilty
				? $"Docs called security — forged papers on {passenger.DisplayName}"
				: $"Docs called security on {passenger.DisplayName} (no forgery found)" );

		if ( agent is not null )
			agent.ActivePrompt = guilty
				? $"Security called — {passenger.DisplayName} detained (forgery confirmed)"
				: $"Security called — {passenger.DisplayName} detained (may have been clean)";

		Finish();
	}

	void Finish()
	{
		Busy = false;
		CurrentPassengerId = null;
		SubmittedJson = null;
		ReferenceJson = null;
		SelectedFieldName = null;
	}
}

public sealed class ScannerStation : Component
{
	public const string NpcIdPrefix = "npc:";

	[Sync( SyncFlags.FromHost )] public string CurrentPassengerId { get; set; }
	[Sync( SyncFlags.FromHost )] public int QueueCount { get; set; }
	[Sync( SyncFlags.FromHost )] public bool Busy { get; set; }
	[Sync( SyncFlags.FromHost )] public bool Searching { get; set; }
	[Sync( SyncFlags.FromHost )] public float SearchTimeLeft { get; set; }
	[Sync( SyncFlags.FromHost )] public string BagJson { get; set; }
	/// <summary>Declared packing list (clean) for side-by-side compare.</summary>
	[Sync( SyncFlags.FromHost )] public string ManifestJson { get; set; }
	/// <summary>Item id that looks wrong on the X-ray (hide spot). Empty if bag looks clean.</summary>
	[Sync( SyncFlags.FromHost )] public string AnomalyItemId { get; set; }
	[Sync( SyncFlags.FromHost )] public string SelectedItemId { get; set; }

	public BagInstance ActiveBag => BagNet.FromJson( BagJson );
	public BagInstance ActiveManifest => BagNet.FromJson( ManifestJson );
	public bool IsNpcJob => CurrentPassengerId?.StartsWith( NpcIdPrefix ) == true;

	TimeSince _searchTimer;
	TimeSince _busyTimer;
	float _searchDuration;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( Searching )
		{
			SearchTimeLeft = MathF.Max( 0f, _searchDuration - _searchTimer );
			if ( SearchTimeLeft <= 0f )
			{
				Searching = false;
				// False search delay ends — passenger still clears so the belt keeps moving.
				CompleteClear( null, silent: true );
			}
			return;
		}

		if ( Busy && _busyTimer > 2f && !HasActiveSubject() )
			Finish();
	}

	public bool BeginScan( NoFlyPlayer passenger )
	{
		if ( !Networking.IsHost || passenger is null || Busy || Searching ) return false;
		passenger.Bag ??= LuggageCatalog.CreateCleanBag( passenger.PlayerId );
		if ( passenger.Bag is null ) return false;

		Busy = true;
		_busyTimer = 0;
		CurrentPassengerId = passenger.PlayerId;
		passenger.FlowState = PassengerFlowState.BagInspection;
		LoadBagOntoBelt( passenger.Bag );
		return true;
	}

	public bool BeginScanNpc( NpcPassenger npc )
	{
		if ( !Networking.IsHost || npc is null || Busy || Searching ) return false;
		npc.Bag ??= LuggageCatalog.CreateCleanBag( npc.NpcId );
		if ( npc.Bag is null ) return false;

		Busy = true;
		_busyTimer = 0;
		CurrentPassengerId = NpcIdPrefix + npc.NpcId;
		npc.FlowState = PassengerFlowState.BagInspection;
		LoadBagOntoBelt( npc.Bag );
		return true;
	}

	void LoadBagOntoBelt( BagInstance bag )
	{
		BagJson = BagNet.ToJson( bag );
		var manifest = new BagInstance
		{
			OwnerId = bag.OwnerId,
			LayoutId = bag.LayoutId,
			BagNumber = bag.BagNumber,
			SuitcaseColor = bag.SuitcaseColor
		};
		ManifestJson = BagNet.ToJson( manifest );
		if ( bag.HasContraband )
		{
			var layout = LuggageCatalog.GetLayout( bag.LayoutId );
			AnomalyItemId = bag.HiddenBehindItemId
				?? layout.Slots.FirstOrDefault()?.ItemId;
		}
		else
		{
			AnomalyItemId = null;
		}

		SelectedItemId = null;
	}

	public void ClearBag( NoFlyPlayer agent )
	{
		if ( !Networking.IsHost || Searching ) return;
		CompleteClear( agent, silent: false );
	}

	void CompleteClear( NoFlyPlayer agent, bool silent )
	{
		var bag = ActiveBag;
		var game = NoFlyGame.Instance;

		if ( TryResolvePlayer( out var passenger ) )
		{
			QueueSystem.Leave( passenger );
			if ( bag?.HasContraband == true )
			{
				if ( !silent )
				{
					agent?.AddScore( -60, "inspection" );
					passenger.AddScore( 100, "deception" );
				}
			}
			else if ( !silent )
			{
				agent?.AddScore( 25, "inspection" );
			}

			passenger.BagCleared = true;
			passenger.FlowState = PassengerFlowState.InTerminal;
			if ( !silent && agent is not null )
				agent.ActivePrompt = $"Bag cleared — {passenger.DisplayName}";
			Finish();
			return;
		}

		if ( TryResolveNpc( out var npc ) )
		{
			if ( bag?.HasContraband == true )
			{
				if ( !silent ) agent?.AddScore( -60, "inspection" );
			}
			else if ( !silent )
			{
				agent?.AddScore( 25, "inspection" );
			}

			npc.FlowState = PassengerFlowState.InTerminal;
			if ( !silent && agent is not null )
				agent.ActivePrompt = $"Bag cleared — traveler {npc.NpcId}";
			Finish();
			return;
		}

		Finish();
	}

	public void SearchBag( NoFlyPlayer agent )
	{
		if ( !Networking.IsHost || Searching ) return;
		var game = NoFlyGame.Instance;
		if ( game is null ) return;

		var bag = ActiveBag;
		var hit = bag?.HasContraband == true
			&& !string.IsNullOrEmpty( SelectedItemId )
			&& (SelectedItemId == bag.HiddenBehindItemId
				|| SelectedItemId == bag.ContrabandId
				|| SelectedItemId == AnomalyItemId);

		if ( hit )
		{
			agent?.AddScore( 120, "inspection" );
			if ( TryResolvePlayer( out var passenger ) )
			{
				QueueSystem.Leave( passenger );
				SecurityCall.Detain( game, passenger, agent, AlertType.BagFlag,
					$"Contraband found in {passenger.DisplayName}'s bag" );
				if ( agent is not null )
					agent.ActivePrompt = $"Search hit — {passenger.DisplayName} detained";
			}
			else if ( TryResolveNpc( out var npc ) )
			{
				npc.FlowState = PassengerFlowState.Detained;
				npc.WorldPosition = game.Airport?.GetSpawn( "holding" ) ?? npc.WorldPosition;
				game.AddAlert( new SecurityAlert
				{
					Type = AlertType.BagFlag,
					Message = $"Contraband found in traveler {npc.NpcId}'s bag",
					SourcePlayerId = agent?.PlayerId,
					Position = npc.WorldPosition
				} );
				if ( agent is not null )
					agent.ActivePrompt = $"Search hit — traveler {npc.NpcId} detained";
			}
			Finish();
			return;
		}

		agent?.AddScore( -40, "inspection" );
		if ( TryResolvePlayer( out var p ) )
			p.AddScore( -15, "boarding" );

		Searching = true;
		_searchDuration = game.Settings?.FalseSearchDelaySeconds ?? 5f;
		_searchTimer = 0f;
		SearchTimeLeft = _searchDuration;
		if ( agent is not null )
			agent.ActivePrompt = "Nothing found — queue delayed";
	}

	public void CallSecurity( NoFlyPlayer agent )
	{
		if ( !Networking.IsHost ) return;
		var game = NoFlyGame.Instance;
		if ( game is null ) return;

		var bag = ActiveBag;
		var guilty = bag?.HasContraband == true;
		if ( guilty )
			agent?.AddScore( 70, "inspection" );
		else
			agent?.AddScore( -35, "inspection" );

		if ( TryResolvePlayer( out var passenger ) )
		{
			QueueSystem.Leave( passenger );
			SecurityCall.Detain( game, passenger, agent, AlertType.BagFlag,
				guilty
					? $"Scanner called security — contraband on {passenger.DisplayName}"
					: $"Scanner called security on {passenger.DisplayName} (bag was clean)" );
			if ( agent is not null )
				agent.ActivePrompt = guilty
					? $"Security called — {passenger.DisplayName} detained (contraband confirmed)"
					: $"Security called — {passenger.DisplayName} detained (may have been clean)";
		}
		else if ( TryResolveNpc( out var npc ) )
		{
			npc.FlowState = PassengerFlowState.Detained;
			npc.WorldPosition = game.Airport?.GetSpawn( "holding" ) ?? npc.WorldPosition;
			game.AddAlert( new SecurityAlert
			{
				Type = AlertType.BagFlag,
				Message = guilty
					? $"Scanner called security — contraband on traveler {npc.NpcId}"
					: $"Scanner called security on traveler {npc.NpcId} (bag was clean)",
				SourcePlayerId = agent?.PlayerId,
				Position = npc.WorldPosition
			} );
			if ( agent is not null )
				agent.ActivePrompt = $"Security called — traveler {npc.NpcId} detained";
		}

		Finish();
	}

	bool HasActiveSubject() => TryResolvePlayer( out _ ) || TryResolveNpc( out _ );

	bool TryResolvePlayer( out NoFlyPlayer passenger )
	{
		passenger = null;
		if ( IsNpcJob || string.IsNullOrEmpty( CurrentPassengerId ) ) return false;
		passenger = NoFlyGame.Instance?.FindPlayer( CurrentPassengerId );
		return passenger.IsValid();
	}

	bool TryResolveNpc( out NpcPassenger npc )
	{
		npc = null;
		if ( !IsNpcJob ) return false;
		var id = CurrentPassengerId[NpcIdPrefix.Length..];
		npc = NoFlyGame.Instance?.Scene?.GetAllComponents<NpcPassenger>()
			.FirstOrDefault( n => n.NpcId == id );
		return npc.IsValid();
	}

	void Finish()
	{
		Busy = false;
		Searching = false;
		CurrentPassengerId = null;
		BagJson = null;
		ManifestJson = null;
		AnomalyItemId = null;
		SelectedItemId = null;
	}
}

/// <summary>Shared detain + tablet alert when Docs/Scanner call security.</summary>
public static class SecurityCall
{
	public static void Detain( NoFlyGame game, NoFlyPlayer passenger, NoFlyPlayer agent, AlertType type, string message )
	{
		if ( game is null || passenger is null ) return;

		passenger.IsFlagged = true;
		passenger.IsDetained = true;
		passenger.FlowState = PassengerFlowState.Detained;
		passenger.ActivePrompt = "Security was called — wait in holding";
		passenger.WorldPosition = game.Airport?.GetSpawn( "holding" ) ?? passenger.WorldPosition;

		game.AddAlert( new SecurityAlert
		{
			Type = type,
			Message = message,
			TargetPlayerId = passenger.PlayerId,
			SourcePlayerId = agent?.PlayerId,
			Position = passenger.WorldPosition
		} );
	}
}

public static class DocNet
{
	public static string ToJson( DocumentInstance doc )
	{
		if ( doc is null ) return null;
		var fields = string.Join( ";", doc.Values.Select( kv => $"{(int)kv.Key}={kv.Value}" ) );
		return $"{doc.OwnerId}|{doc.TemplateId}|{(int?)doc.ForgedField}|{doc.OriginalValue}|{doc.ForgedValue}|{(int)doc.Difficulty}|{fields}";
	}

	public static DocumentInstance FromJson( string json )
	{
		if ( string.IsNullOrEmpty( json ) ) return null;
		var parts = json.Split( '|' );
		if ( parts.Length < 7 ) return null;
		var doc = new DocumentInstance
		{
			OwnerId = parts[0],
			TemplateId = parts[1],
			OriginalValue = parts[3],
			ForgedValue = parts[4],
			Difficulty = (DiscrepancyDifficulty)int.Parse( parts[5] )
		};
		if ( int.TryParse( parts[2], out var ff ) ) doc.ForgedField = (DocumentFieldType)ff;
		foreach ( var pair in parts[6].Split( ';', StringSplitOptions.RemoveEmptyEntries ) )
		{
			var kv = pair.Split( '=', 2 );
			if ( kv.Length == 2 && int.TryParse( kv[0], out var key ) )
				doc.Values[(DocumentFieldType)key] = kv[1];
		}
		return doc;
	}

	public static DocumentInstance Clone( DocumentInstance src ) => FromJson( ToJson( src ) );
}

public static class BagNet
{
	public static string ToJson( BagInstance bag )
	{
		if ( bag is null ) return null;
		return $"{bag.OwnerId}|{bag.LayoutId}|{bag.BagNumber}|{bag.ContrabandId}|{bag.HiddenBehindItemId}|{bag.ContrabandOffset.x}|{bag.ContrabandOffset.y}|{bag.SuitcaseColor.Hex}";
	}

	public static BagInstance FromJson( string json )
	{
		if ( string.IsNullOrEmpty( json ) ) return null;
		var p = json.Split( '|' );
		if ( p.Length < 8 ) return null;
		return new BagInstance
		{
			OwnerId = p[0],
			LayoutId = p[1],
			BagNumber = p[2],
			ContrabandId = string.IsNullOrEmpty( p[3] ) ? null : p[3],
			HiddenBehindItemId = string.IsNullOrEmpty( p[4] ) ? null : p[4],
			ContrabandOffset = new Vector2( float.Parse( p[5] ), float.Parse( p[6] ) ),
			SuitcaseColor = Color.Parse( p[7] ) ?? Color.White
		};
	}
}
