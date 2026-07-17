namespace DeepDive;

/// <summary>Environmental story fragment — scanned or touched to unlock journal text.</summary>
public sealed class StoryPickup : Component
{
	public StoryDefinition Definition { get; private set; }
	public bool Collected { get; private set; }
	public float InteractRadius { get; set; } = 3.2f;

	public void Setup( StoryDefinition def )
	{
		Definition = def;
		var go = new GameObject( GameObject, true, "Visual" );
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( 1.1f, 0.4f, 1.4f ) );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = def?.Tint ?? Color.Cyan;
	}

	public bool TryInteract( DeepDiveGame game )
	{
		if ( Collected || Definition is null || game is null )
			return false;

		if ( Definition.RequiredTool is ToolKind required )
		{
			var selected = game.Tools?.Hotbar.SelectedTool?.Kind;
			if ( selected != required && game.Tools?.ScannerActive != true )
			{
				game.ShowMessage( $"Needs {required}", 1.1f );
				return false;
			}
		}

		Collected = true;
		var first = game.Progression.RegisterStory( Definition.Id );
		game.ShowMessage( first ? $"Log: {Definition.Title}" : Definition.Title, 1.6f );
		game.DiveLog?.AddEntry( Definition.Title, "/ui/icons/map_objective.png", known: true );
		GameObject.Destroy();
		return true;
	}

	protected override void OnUpdate()
	{
		if ( Collected || Definition is null ) return;
		var game = DeepDiveGame.Instance;
		if ( game is null || !game.State.IsDivingActive ) return;
		if ( Definition.RequiredTool is not null ) return; // must interact intentionally

		var diver = game.Diver;
		if ( diver is null ) return;
		if ( (diver.WorldPosition - WorldPosition).Length > InteractRadius ) return;
		TryInteract( game );
	}
}
