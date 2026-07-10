namespace UnderPressure;

/// <summary>
/// Loads a real animal model for a pest. Falls back to tinted box primitives when the
/// asset path is missing so jobs stay playable during art pass.
/// </summary>
public sealed class AnimalPestVisual : Component
{
	public const string BodyChildName = "Body";

	private GameObject _body;
	private ModelRenderer _renderer;
	private bool _ready;

	public bool IsReady => _ready;

	/// <summary>Attach an animal mesh to this pest. Returns false when the model is unavailable.</summary>
	public bool TrySetup( string modelPath, float scale, Color tint )
	{
		if ( _ready )
			return true;

		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		var model = Model.Load( modelPath );
		if ( model is null || !model.IsValid || model.IsError )
			return false;

		_body = FindNamedChild( GameObject, BodyChildName );
		if ( !_body.IsValid() )
		{
			_body = new GameObject( true, BodyChildName );
			_body.SetParent( GameObject );
		}

		_body.LocalPosition = Vector3.Zero;
		_body.LocalRotation = Rotation.Identity;
		_body.LocalScale = Vector3.One * Math.Max( 0.2f, scale );

		_renderer = _body.Components.Get<ModelRenderer>() ?? _body.Components.Create<ModelRenderer>();
		_renderer.Model = model;
		_renderer.Tint = tint;
		_renderer.Enabled = true;

		_ready = true;
		return true;
	}

	static GameObject FindNamedChild( GameObject root, string name )
	{
		if ( root is null || !root.IsValid() || string.IsNullOrWhiteSpace( name ) )
			return default;

		foreach ( var child in root.Children )
		{
			if ( child.IsValid() && string.Equals( child.Name, name, StringComparison.OrdinalIgnoreCase ) )
				return child;
		}

		return default;
	}
}
