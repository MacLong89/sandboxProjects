namespace Terraingen.UI.Components;



using Sandbox;

using Sandbox.UI;

using Terraingen.Animals;



// Renders a ScenePanel preview of a .vmdl with optional idle anim and spin.

public sealed class ThornsModelPreviewPanel : Panel

{

	const float DefaultYawDegrees = 28f;

	const float DefaultSpinYawDegreesPerSecond = 22f;

	const float HeroVisualScale = 0.32f;



	ScenePanel _scenePanel;

	Scene _previewScene;

	CameraComponent _camera;

	GameObject _modelRoot;

	SkinnedModelRenderer _renderer;

	string _modelPath = "";

	string _idleSequence = "";

	bool _hasModel;

	bool _spinEnabled;

	bool _heroFraming;

	bool _transparentBackground;

	float _baseYawDegrees = DefaultYawDegrees;

	float _spinYawDegrees;

	float _spinYawDegreesPerSecond = DefaultSpinYawDegreesPerSecond;

	float _visualScale = ThornsAnimalManager.VisualScale;

	float _renderTimer;



	public ThornsModelPreviewPanel( Panel parent, string cssClass = "thorns-model-preview", int? widthPx = null, int? heightPx = null )

	{

		Parent = parent;

		if ( !string.IsNullOrWhiteSpace( cssClass ) )

			AddClass( cssClass );



		if ( widthPx.HasValue )

			Style.Width = Length.Pixels( widthPx.Value );



		if ( heightPx.HasValue )

			Style.Height = Length.Pixels( heightPx.Value );



		Style.Overflow = OverflowMode.Hidden;

		Style.BackgroundColor = new Color( 0.06f, 0.08f, 0.11f, 1f );

		Style.FlexShrink = 0;



		_previewScene = Scene.CreateEditorScene();

		_previewScene.WantsSystemScene = false;



		_scenePanel = new ScenePanel();

		_scenePanel.Style.Width = Length.Percent( 100 );

		_scenePanel.Style.Height = Length.Percent( 100 );

		_scenePanel.Style.FlexGrow = 1;

		_scenePanel.Style.FlexShrink = 1;

		_scenePanel.Style.PointerEvents = PointerEvents.None;

		_scenePanel.RenderScene = _previewScene;

		AddChild( _scenePanel );



		using ( _previewScene.Push() )

		{

			BuildCamera();

			BuildLights();



			_modelRoot = _previewScene.CreateObject( true );

			_modelRoot.Name = "Thorns Model Preview";

			_renderer = _modelRoot.AddComponent<SkinnedModelRenderer>();

			_renderer.UseAnimGraph = false;

			_renderer.CreateBoneObjects = false;

		}

	}



	public bool SetModel( string modelPath, ThornsModelPreviewPresentation presentation = null )

	{

		presentation ??= ThornsModelPreviewPresentation.Default;

		modelPath = modelPath?.Trim() ?? "";



		ApplyPresentation( presentation );



		if ( string.Equals( modelPath, _modelPath, StringComparison.OrdinalIgnoreCase )

		     && _hasModel

		     && string.Equals( _idleSequence, presentation.IdleSequence ?? "", StringComparison.OrdinalIgnoreCase ) )

			return true;



		_modelPath = modelPath;

		_idleSequence = presentation.IdleSequence ?? "";

		_spinYawDegrees = 0f;

		ClearModel();



		if ( string.IsNullOrWhiteSpace( modelPath ) )

		{

			RequestRender();

			return false;

		}



		var model = Model.Load( modelPath );

		if ( !model.IsValid() )

		{

			Log.Warning( $"[Thorns UI] Model preview failed to load: '{modelPath}'." );

			RequestRender();

			return false;

		}



		using ( _previewScene.Push() )

		{

			_renderer.Model = model;

			_modelRoot.WorldPosition = -model.Bounds.Center * _visualScale;

			_modelRoot.WorldRotation = Rotation.FromYaw( _baseYawDegrees );

			_modelRoot.WorldScale = _visualScale;

			ApplyIdleSequence( _idleSequence );

		}



		FrameCamera( model.Bounds, _visualScale );

		_hasModel = true;

		RequestRender();

		return true;

	}



	public void Clear()

	{

		_modelPath = "";

		_idleSequence = "";

		_spinYawDegrees = 0f;

		ClearModel();

		RequestRender();

	}



	public override void Tick()

	{

		base.Tick();



		if ( !_hasModel || !_modelRoot.IsValid() || Style.Display == DisplayMode.None )

			return;



		if ( _spinEnabled )

		{

			_spinYawDegrees += _spinYawDegreesPerSecond * Time.Delta;

			using ( _previewScene.Push() )

				_modelRoot.WorldRotation = Rotation.FromYaw( _baseYawDegrees + _spinYawDegrees );

		}



		_renderTimer -= Time.Delta;

		if ( _renderTimer > 0f )

			return;



		_renderTimer = Terraingen.Core.ThornsHudTickRates.ModelPreviewRenderSeconds;

		RequestRender();

	}



	public override void OnDeleted()

	{

		if ( _previewScene.IsValid() )

			_previewScene.Destroy();



		base.OnDeleted();

	}



	void ApplyPresentation( ThornsModelPreviewPresentation presentation )

	{

		_spinEnabled = presentation.AutoSpin;

		_heroFraming = presentation.HeroFraming;

		_transparentBackground = presentation.TransparentBackground;

		_baseYawDegrees = presentation.BaseYawDegrees;

		_spinYawDegreesPerSecond = presentation.SpinYawDegreesPerSecond;

		_visualScale = presentation.HeroFraming ? HeroVisualScale : ThornsAnimalManager.VisualScale;



		if ( presentation.TransparentBackground )

			Style.BackgroundColor = Color.Transparent;

		else

			Style.BackgroundColor = new Color( 0.06f, 0.08f, 0.11f, 1f );



		ApplyCameraBackground();

	}



	void ApplyCameraBackground()

	{

		var bg = _transparentBackground

			? new Color( 0.06f, 0.08f, 0.11f, 1f )

			: new Color( 0.06f, 0.08f, 0.11f, 1f );



		if ( !_camera.IsValid() )

			return;



		using ( _previewScene.Push() )

		{

			_camera.BackgroundColor = bg;

			_camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth;

		}

	}



	void ApplyIdleSequence( string idleSequence )

	{

		if ( !_renderer.IsValid() || string.IsNullOrWhiteSpace( idleSequence ) )

			return;



		_renderer.Sequence.Name = idleSequence;

		_renderer.Sequence.Looping = true;

		_renderer.PlaybackRate = 1f;

	}



	void BuildCamera()

	{

		if ( !_previewScene.Camera.IsValid() )

		{

			var camGo = _previewScene.CreateObject( true );

			camGo.Name = "Thorns Model Preview Camera";

			camGo.AddComponent<CameraComponent>();

		}



		_camera = _previewScene.Camera;

		if ( !_camera.IsValid() )

			return;



		using ( _previewScene.Push() )

		{

			_camera.FieldOfView = 24f;

			_camera.ZNear = 5f;

			_camera.ZFar = 8000f;

			_camera.BackgroundColor = new Color( 0.06f, 0.08f, 0.11f, 1f );

			_camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth;

		}

	}



	void BuildLights()

	{

		if ( _previewScene.SceneWorld is not null )

			_previewScene.SceneWorld.AmbientLightColor = new Color( 0.42f, 0.44f, 0.48f );



		AddPointLight( new Vector3( 120f, 80f, 140f ), 500f, Color.White * 1.4f );

		AddPointLight( new Vector3( -90f, -60f, 100f ), 280f, new Color( 0.55f, 0.62f, 0.75f ) * 0.9f );

	}



	void AddPointLight( Vector3 position, float radius, Color color )

	{

		var go = _previewScene.CreateObject( true );

		go.WorldPosition = position;

		var light = go.AddComponent<PointLight>();

		light.LightColor = color;

		light.Radius = radius;

	}



	void ClearModel()

	{

		if ( _renderer.IsValid() )

			_renderer.Model = null;



		_hasModel = false;

	}



	void FrameCamera( BBox bounds, float scale )

	{

		if ( !_camera.IsValid() )

			return;



		var size = bounds.Size * scale;

		var radius = Math.Max( size.x, Math.Max( size.y, size.z ) );

		if ( radius < 1f )

			radius = 48f;



		var distance = radius * ( _heroFraming ? 3.9f : 2.15f );

		var camPos = new Vector3( distance * 0.78f, distance * 0.42f, distance * 0.28f );

		var camRot = Rotation.LookAt( (Vector3.Zero - camPos).Normal, Vector3.Up );

		using ( _previewScene.Push() )

		{

			_camera.GameObject.WorldPosition = camPos;

			_camera.GameObject.WorldRotation = camRot;

			_camera.FieldOfView = _heroFraming ? 22f : 24f;

		}

	}



	void RequestRender()

	{

		if ( _scenePanel.IsValid() )

			_scenePanel.RenderNextFrame();

	}

}



public sealed record ThornsModelPreviewPresentation

{

	public static ThornsModelPreviewPresentation Default { get; } = new();



	public static ThornsModelPreviewPresentation TameHero { get; } = new()

	{

		AutoSpin = true,

		TransparentBackground = true,

		HeroFraming = true,

		BaseYawDegrees = 18f,

		SpinYawDegreesPerSecond = 22f

	};



	public bool AutoSpin { get; init; }

	public bool TransparentBackground { get; init; }

	public bool HeroFraming { get; init; }

	public float BaseYawDegrees { get; init; } = 28f;

	public float SpinYawDegreesPerSecond { get; init; } = 22f;

	public string IdleSequence { get; init; } = "";

}


