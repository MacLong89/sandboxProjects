namespace UnderPressure;

/// <summary>One cleaning pass on a surface: a layer that a specific tool removes. Layers stack
/// (index 0 on top) so cleaning one reveals the next — e.g. the pressure washer strips grime
/// and exposes a wet film, which the squeegee then wipes away.</summary>
public readonly struct CleanStage
{
	public ToolType Tool { get; init; }
	public Color Color { get; init; }

	/// <summary>Wet layers render as a thin translucent film (residue); dry layers are opaque grime.</summary>
	public bool Wet { get; init; }
}

/// <summary>
/// A rectangular dirty panel. Instead of thousands of tiny tile objects, each cleaning layer
/// is a single quad textured with a runtime "mask": every texel starts opaque and fades to
/// transparent as it's cleaned, so the effective tile size is as fine as the texture
/// resolution. Layers stack so multi-tool jobs read naturally — pressure-wash the grime off
/// to reveal a wet film, then squeegee the film away. Orientation (floor vs wall vs window)
/// is driven entirely by the owning GameObject's transform; masks map 1:1 to the local plane.
/// Pests can smear grime back onto the base (dirt) layer via <see cref="Resoil"/>.
/// </summary>
public sealed class CleanableSurface : Component
{
	public float Width { get; private set; }
	public float Height { get; private set; }

	/// <summary>World area of a single mask texel (keeps payouts tile-size independent).</summary>
	public float CellArea => (Width / _texW) * (Height / _texH);

	/// <summary>Total cleanable units summed across every layer.</summary>
	public int TotalCells { get; private set; }
	public int CleanedCount { get; private set; }
	public bool IsClean => CleanedCount >= TotalCells && TotalCells > 0;

	/// <summary>The tool needed right now: the first unfinished layer's tool (top-down).</summary>
	public ToolType ActiveTool
	{
		get
		{
			foreach ( var tool in ToolsWithWork() )
				return tool;
			return _layers.Count > 0 ? _layers[^1].Tool : ToolType.PressureWasher;
		}
	}

	/// <summary>Tools that still have cleanable texels on this surface (respecting per-texel
	/// layer unlock — a follow-up tool is available wherever the prior layer is already clear).</summary>
	public IEnumerable<ToolType> ToolsWithWork()
	{
		for ( var i = 0; i < _layers.Count; i++ )
		{
			if ( LayerHasWork( i ) )
				yield return _layers[i].Tool;
		}
	}

	/// <summary>True if <paramref name="tool"/> can still clean something here.</summary>
	public bool HasWorkFor( ToolType tool )
	{
		var idx = LayerIndex( tool );
		return idx >= 0 && LayerHasWork( idx );
	}

	/// <summary>Raised with the count of texels that became fully clean this call.</summary>
	public event Action<int> CellsCleaned;

	/// <summary>Raised with the count of clean texels that got dirtied again (by pests).</summary>
	public event Action<int> CellsResoiled;

	// Mask density: texels per world unit. ~1.2 makes each texel well under a unit across,
	// far finer than the old ~13-unit tiles, while the cap keeps memory/perf bounded.
	private const float MaskDensity = 1.2f;
	private const int MaskCap = 512;
	private const byte WetAlpha = 120;   // residue films are thin, so never fully opaque

	private int _texW;
	private int _texH;

	/// <summary>One stacked cleaning pass, with its own mask texture and progress.</summary>
	private sealed class Layer
	{
		public ToolType Tool;
		public byte MaxAlpha;
		public float[] Clean;      // 0 = fully present, 1 = fully removed
		public Color32[] Pixels;   // RGB = baked shade, A = current opacity
		public Color32[] Scratch;  // reusable upload buffer for the touched sub-rect
		public Texture Texture;
		public int Total;
		public int Cleaned;
		public bool Done => Cleaned >= Total;
	}

	private readonly List<Layer> _layers = new();

	private sealed class DiscoverySlot
	{
		public string Id;
		public string Monologue;
		public int MinX, MinY, MaxX, MaxY;
		public float Threshold;
		public bool Fired;
	}

	private readonly List<DiscoverySlot> _discoveries = new();
	private bool[] _active;
	private PanelShape _shape = PanelShape.Full;

	public void Setup( float width, float height, float cellSize, Color cleanColor, Material cleanMaterial, IReadOnlyList<CleanStage> stages, IReadOnlyList<GraffitiLine> graffiti = null, IReadOnlyList<SurfaceSecret> secrets = null, PanelShape shape = PanelShape.Full, GrimePattern grimePattern = GrimePattern.Organic )
	{
		Width = width;
		Height = height;
		_shape = shape;

		// cellSize is no longer a geometry step; it only nudges the mask resolution so
		// jobs that used bigger tiles still read a touch coarser. Mostly we go by density.
		_texW = Math.Clamp( (int)MathF.Round( width * MaskDensity ), 8, MaskCap );
		_texH = Math.Clamp( (int)MathF.Round( height * MaskDensity ), 8, MaskCap );
		var cellsPerLayer = _texW * _texH;

		_active = BuildActiveMask( shape );

		BuildCleanBase( cleanColor, cleanMaterial ?? GameMaterials.Concrete );
		if ( secrets is { Count: > 0 } || graffiti is { Count: > 0 } )
			BuildUnderlayLayer( secrets, graffiti, cleanColor );
		BuildCollider();
		RegisterDiscoveries( secrets, graffiti );

		var count = Math.Max( 1, stages.Count );
		for ( var i = 0; i < count; i++ )
		{
			var spec = stages[i];
			var layer = new Layer
			{
				Tool = spec.Tool,
				MaxAlpha = spec.Wet ? WetAlpha : (byte)255,
				Clean = new float[cellsPerLayer],
				Pixels = new Color32[cellsPerLayer],
				Total = cellsPerLayer,
			};

			BakeLayer( layer, spec.Color, grimePattern, _texW, _texH );
			ApplyShapeMask( layer );

			// Layer 0 sits on top (highest Z) and is cleaned first; later layers are beneath.
			var z = DepthLayers.GrimeLayerZ( i, count );
			BuildLayerPlane( layer, z );

			_layers.Add( layer );
			TotalCells += layer.Total;
		}

		GameObject.Tags.Add( "cleanable" );
	}

	private bool[] BuildActiveMask( PanelShape shape )
	{
		var mask = new bool[_texW * _texH];
		var aspect = Width / Math.Max( Height, 1f );

		for ( var y = 0; y < _texH; y++ )
		for ( var x = 0; x < _texW; x++ )
		{
			var u = (x + 0.5f) / _texW;
			var v = (y + 0.5f) / _texH;
			mask[y * _texW + x] = PanelShapeMask.IsActive( shape, u, v, aspect );
		}

		return mask;
	}

	private static void BakeLayer( Layer layer, Color color, GrimePattern pattern, int texW, int texH )
	{
		for ( var y = 0; y < texH; y++ )
		for ( var x = 0; x < texW; x++ )
		{
			var i = y * texW + x;
			var shade = PanelShapeMask.GrimeShade( pattern, x, y, texW, texH );
			layer.Pixels[i] = new Color32(
				(byte)Math.Clamp( (int)(color.r * shade * 255f), 0, 255 ),
				(byte)Math.Clamp( (int)(color.g * shade * 255f), 0, 255 ),
				(byte)Math.Clamp( (int)(color.b * shade * 255f), 0, 255 ),
				layer.MaxAlpha );
		}
	}

	private void ApplyShapeMask( Layer layer )
	{
		var active = 0;

		for ( var i = 0; i < layer.Total; i++ )
		{
			if ( _active[i] )
			{
				active++;
				continue;
			}

			layer.Clean[i] = 1f;
			layer.Pixels[i].a = 0;
		}

		layer.Total = active;
		layer.Cleaned = 0;
	}

	private void MaskPixels( Color32[] pixels )
	{
		if ( _active is null )
			return;

		for ( var i = 0; i < pixels.Length; i++ )
		{
			if ( !_active[i] )
				pixels[i] = default;
		}
	}

	private void BuildCleanBase( Color cleanColor, Material cleanMaterial )
	{
		var baseGo = new GameObject( GameObject, true, "CleanBase" );
		baseGo.LocalPosition = new Vector3( 0f, 0f, DepthLayers.CleanBase );

		if ( _shape == PanelShape.Full )
		{
			baseGo.LocalScale = MeshPrimitives.BoxScale( new Vector3( Width, Height, 2f ) );
			var mr = baseGo.Components.Create<ModelRenderer>();
			mr.Model = MeshPrimitives.Box;
			mr.MaterialOverride = cleanMaterial;
			mr.Tint = cleanColor;
			return;
		}

		var pixels = new Color32[_texW * _texH];
		for ( var i = 0; i < pixels.Length; i++ )
		{
			if ( !_active[i] )
			{
				pixels[i] = default;
				continue;
			}

			pixels[i] = new Color32(
				(byte)Math.Clamp( (int)(cleanColor.r * 255f), 0, 255 ),
				(byte)Math.Clamp( (int)(cleanColor.g * 255f), 0, 255 ),
				(byte)Math.Clamp( (int)(cleanColor.b * 255f), 0, 255 ),
				255 );
		}

		var texture = Texture.Create( _texW, _texH, ImageFormat.RGBA8888 ).Finish();
		texture.Update( pixels, 0, 0, _texW, _texH );

		var src = Material.Load( "materials/up/grime_fade.vmat" );
		var mat = src?.CreateCopy() ?? GameMaterials.GrimeFade;
		mat.Set( "Color", texture );

		var maskMr = baseGo.Components.Create<ModelRenderer>();
		maskMr.Model = BuildMaskQuad( mat );
		maskMr.MaterialOverride = mat;
		maskMr.Tint = Color.White;
	}

	private void BuildUnderlayLayer( IReadOnlyList<SurfaceSecret> secrets, IReadOnlyList<GraffitiLine> graffiti, Color cleanColor )
	{
		var pixels = new Color32[_texW * _texH];
		SecretRaster.Apply( pixels, _texW, _texH, secrets, cleanColor );
		if ( graffiti is { Count: > 0 } )
			GraffitiRaster.Apply( pixels, _texW, _texH, graffiti );
		MaskPixels( pixels );

		var texture = Texture.Create( _texW, _texH, ImageFormat.RGBA8888 ).Finish();
		texture.Update( pixels, 0, 0, _texW, _texH );

		var src = Material.Load( "materials/up/grime_fade.vmat" );
		var mat = src?.CreateCopy() ?? GameMaterials.GrimeFade;
		mat.Set( "Color", texture );

		var go = new GameObject( GameObject, true, "Underlay" );
		go.LocalPosition = new Vector3( 0, 0, DepthLayers.UnderlayLayer );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = BuildMaskQuad( mat );
		mr.MaterialOverride = mat;
		mr.Tint = Color.White;
	}

	private void BuildCollider()
	{
		var col = Components.Create<BoxCollider>();
		col.Center = Vector3.Zero;
		col.Scale = new Vector3( Width, Height, 6f );
		col.Static = true;
	}

	private void BuildLayerPlane( Layer layer, float z )
	{
		layer.Texture = Texture.Create( _texW, _texH, ImageFormat.RGBA8888 ).Finish();
		layer.Texture.Update( layer.Pixels, 0, 0, _texW, _texH );

		// Copy the translucent grime material so each layer binds its own mask texture.
		var src = Material.Load( "materials/up/grime_fade.vmat" );
		var mat = src?.CreateCopy() ?? GameMaterials.GrimeFade;
		mat.Set( "Color", layer.Texture );

		var go = new GameObject( GameObject, true, "Layer" );
		go.LocalPosition = new Vector3( 0, 0, z );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = BuildMaskQuad( mat );
		mr.MaterialOverride = mat;
		mr.Tint = Color.White;
	}

	/// <summary>
	/// Build the mask quad as a procedural mesh (centered on the panel's local XY plane) whose
	/// UVs map linearly to the panel so a hit at local (x, y) samples exactly the texel that
	/// <see cref="CleanAt"/> erases: u = x / Width + 0.5, v = y / Height + 0.5. Defining the
	/// UVs ourselves keeps the cleaned spot locked to the crosshair, instead of depending on
	/// whatever UV layout a shared dev-plane model happens to ship with.
	/// </summary>
	private Model BuildMaskQuad( Material material )
	{
		var hw = Width * 0.5f;
		var hh = Height * 0.5f;
		var n = Vector3.Up;       // +Z: the panel faces up in its own local space
		var t = Vector3.Forward;  // +X: tangent runs along U

		var vb = new VertexBuffer();
		vb.Init( true );
		vb.Add( new Vertex( new Vector3( -hw, -hh, 0f ), n, t, new Vector4( 0f, 0f, 0f, 0f ) ) );
		vb.Add( new Vertex( new Vector3(  hw, -hh, 0f ), n, t, new Vector4( 1f, 0f, 0f, 0f ) ) );
		vb.Add( new Vertex( new Vector3(  hw,  hh, 0f ), n, t, new Vector4( 1f, 1f, 0f, 0f ) ) );
		vb.Add( new Vertex( new Vector3( -hw,  hh, 0f ), n, t, new Vector4( 0f, 1f, 0f, 0f ) ) );

		foreach ( var i in new[] { 0, 1, 2, 0, 2, 3 } )
			vb.AddRawIndex( i );

		var mesh = new Mesh( material );
		mesh.CreateBuffers( vb );

		return new ModelBuilder().AddMesh( mesh ).Create();
	}

	/// <summary>
	/// Apply cleaning at a world point with <paramref name="tool"/>. Each layer unlocks per texel:
	/// a follow-up tool can work wherever the prior layer is already clear, without waiting for
	/// the whole panel (or job) to finish the first pass. Returns texels newly fully-cleaned.
	/// </summary>
	public int CleanAt( Vector3 worldPoint, float radius, float amount, bool square, ToolType tool )
	{
		var layerIdx = LayerIndex( tool );
		if ( layerIdx < 0 )
			return 0;

		var layer = _layers[layerIdx];

		if ( !Region( worldPoint, radius, out var fx, out var fy, out var rx, out var ry,
			out var minX, out var minY, out var maxX, out var maxY ) )
			return 0;

		var completed = 0;

		for ( var y = minY; y <= maxY; y++ )
		for ( var x = minX; x <= maxX; x++ )
		{
			var dx = (x + 0.5f - fx) / rx;
			var dy = (y + 0.5f - fy) / ry;

			// Square footprint uses Chebyshev distance (max axis); round uses Euclidean.
			float falloff;
			if ( square )
			{
				var cheb = MathF.Max( MathF.Abs( dx ), MathF.Abs( dy ) );
				if ( cheb > 1f ) continue;
				falloff = 1f - cheb;
			}
			else
			{
				var d2 = dx * dx + dy * dy;
				if ( d2 > 1f ) continue;
				falloff = 1f - MathF.Sqrt( d2 );
			}

			var idx = y * _texW + x;
			if ( !_active[idx] ) continue;
			if ( layer.Clean[idx] >= 1f ) continue;
			if ( !PriorLayersClean( layerIdx, idx ) ) continue;

			layer.Clean[idx] = Math.Clamp( layer.Clean[idx] + amount * (0.35f + 0.65f * falloff), 0f, 1f );
			layer.Pixels[idx].a = (byte)Math.Clamp( (int)((1f - layer.Clean[idx]) * layer.MaxAlpha), 0, 255 );

			if ( layer.Clean[idx] >= 1f )
			{
				layer.Cleaned++;
				CleanedCount++;
				completed++;
			}
		}

		UploadRegion( layer, minX, minY, maxX, maxY );

		if ( completed > 0 )
			CellsCleaned?.Invoke( completed );

		CheckDiscoveries();
		return completed;
	}

	/// <summary>Instantly clear every layer (dev/cheat).</summary>
	public int InstantClean()
	{
		if ( _layers.Count == 0 )
			return 0;

		var completed = 0;
		// Total is the active-cell count after shape masking — walk the full texel buffer.
		var texelCount = _texW * _texH;

		for ( var layerIdx = 0; layerIdx < _layers.Count; layerIdx++ )
		{
			var layer = _layers[layerIdx];

			for ( var i = 0; i < texelCount; i++ )
			{
				if ( _active is not null && !_active[i] )
					continue;
				if ( layer.Clean[i] >= 1f )
					continue;

				layer.Clean[i] = 1f;
				layer.Pixels[i].a = 0;
				layer.Cleaned++;
				CleanedCount++;
				completed++;
			}

			UploadRegion( layer, 0, 0, _texW - 1, _texH - 1 );
		}

		if ( completed > 0 )
			CellsCleaned?.Invoke( completed );

		CheckDiscoveries();
		return completed;
	}

	/// <summary>
	/// Undo cleaning around a world point on the base (dirt) layer — a pest smearing grime back
	/// on. Returns how many texels went from fully-clean back to dirty (so job progress drops).
	/// </summary>
	public int Resoil( Vector3 worldPoint, float radius, float amount )
	{
		if ( _layers.Count == 0 )
			return 0;

		var layer = _layers[0];

		if ( !Region( worldPoint, radius, out var fx, out var fy, out var rx, out var ry,
			out var minX, out var minY, out var maxX, out var maxY ) )
			return 0;

		var dirtied = 0;

		for ( var y = minY; y <= maxY; y++ )
		for ( var x = minX; x <= maxX; x++ )
		{
			var dx = (x + 0.5f - fx) / rx;
			var dy = (y + 0.5f - fy) / ry;
			if ( dx * dx + dy * dy > 1f ) continue;

			var idx = y * _texW + x;
			if ( !_active[idx] ) continue;
			if ( layer.Clean[idx] <= 0f ) continue;

			var wasFull = layer.Clean[idx] >= 1f;
			layer.Clean[idx] = Math.Clamp( layer.Clean[idx] - amount, 0f, 1f );
			layer.Pixels[idx].a = (byte)Math.Clamp( (int)((1f - layer.Clean[idx]) * layer.MaxAlpha), 0, 255 );

			if ( wasFull )
			{
				layer.Cleaned = Math.Max( 0, layer.Cleaned - 1 );
				CleanedCount = Math.Max( 0, CleanedCount - 1 );
				dirtied++;
			}
		}

		UploadRegion( layer, minX, minY, maxX, maxY );

		if ( dirtied > 0 )
			CellsResoiled?.Invoke( dirtied );

		return dirtied;
	}

	/// <summary>
	/// Splatter blood evidence on the base layer — dirties clean cells and tints the whole
	/// patch crimson. Used when a contract target is eliminated.
	/// </summary>
	public int SplatterBlood( Vector3 worldPoint, float radius, float amount = 1f )
	{
		if ( _layers.Count == 0 )
			return 0;

		var layer = _layers[0];

		if ( !Region( worldPoint, radius, out var fx, out var fy, out var rx, out var ry,
			out var minX, out var minY, out var maxX, out var maxY ) )
			return 0;

		var dirtied = 0;

		for ( var y = minY; y <= maxY; y++ )
		for ( var x = minX; x <= maxX; x++ )
		{
			var dx = (x + 0.5f - fx) / rx;
			var dy = (y + 0.5f - fy) / ry;
			var d2 = dx * dx + dy * dy;
			if ( d2 > 1f ) continue;

			var falloff = 1f - MathF.Sqrt( d2 );
			var idx = y * _texW + x;
			if ( !_active[idx] ) continue;
			var wasClean = layer.Clean[idx] >= 1f;

			if ( layer.Clean[idx] > 0f )
			{
				layer.Clean[idx] = Math.Clamp( layer.Clean[idx] - amount * (0.45f + 0.55f * falloff), 0f, 1f );
				if ( wasClean && layer.Clean[idx] < 1f )
				{
					layer.Cleaned = Math.Max( 0, layer.Cleaned - 1 );
					CleanedCount = Math.Max( 0, CleanedCount - 1 );
					dirtied++;
				}
			}

			var shade = 0.72f + Game.Random.Float( 0f, 0.42f ) * falloff;
			var blood = GameConstants.BloodColor;
			layer.Pixels[idx] = new Color32(
				(byte)Math.Clamp( (int)(blood.r * shade * 255f), 0, 255 ),
				(byte)Math.Clamp( (int)(blood.g * shade * 255f), 0, 255 ),
				(byte)Math.Clamp( (int)(blood.b * shade * 255f), 0, 255 ),
				layer.MaxAlpha );
		}

		UploadRegion( layer, minX, minY, maxX, maxY );

		if ( dirtied > 0 )
			CellsResoiled?.Invoke( dirtied );

		return dirtied;
	}

	/// <summary>Splatter blood on whichever cleanable surface is nearest to <paramref name="worldPos"/>.</summary>
	public static void SplatterBloodAt( Scene scene, Vector3 worldPos, float radius = GameConstants.BloodSplatterRadius )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		CleanableSurface best = null;
		var bestDist = float.MaxValue;

		foreach ( var s in scene.GetAllComponents<CleanableSurface>() )
		{
			var d = worldPos.DistanceSquared( s.WorldPosition );
			if ( d < bestDist )
			{
				bestDist = d;
				best = s;
			}
		}

		best?.SplatterBlood( worldPos, radius );
	}

	private void RegisterDiscoveries( IReadOnlyList<SurfaceSecret> secrets, IReadOnlyList<GraffitiLine> graffiti )
	{
		_discoveries.Clear();

		if ( secrets is not null )
		{
			foreach ( var secret in secrets )
				TryRegisterDiscovery( secret.DiscoveryId, secret.Monologue, secret.RevealThreshold,
					secret.X, secret.Y, secret.Scale, secret.Text, secret.Symbol, secret.Centered );
		}

		if ( graffiti is not null )
		{
			foreach ( var line in graffiti )
				TryRegisterDiscovery( line.DiscoveryId, line.Monologue, line.RevealThreshold,
					line.X, line.Y, line.Scale, line.Text, null, line.Centered );
		}
	}

	private void TryRegisterDiscovery( string id, string monologue, float threshold,
		float x, float y, float scale, string text, SecretSymbol? symbol, bool centered )
	{
		if ( string.IsNullOrWhiteSpace( id ) || string.IsNullOrWhiteSpace( monologue ) )
			return;

		SecretRaster.EstimateBounds( _texW, _texH, x, y, scale, text, symbol, centered,
			out var minX, out var minY, out var maxX, out var maxY );

		_discoveries.Add( new DiscoverySlot
		{
			Id = id,
			Monologue = monologue,
			MinX = minX,
			MinY = minY,
			MaxX = maxX,
			MaxY = maxY,
			Threshold = Math.Clamp( threshold, 0.2f, 1f ),
		} );
	}

	private void CheckDiscoveries()
	{
		if ( _discoveries.Count == 0 || _layers.Count == 0 )
			return;

		foreach ( var slot in _discoveries )
		{
			if ( slot.Fired )
				continue;

			if ( RegionRevealProgress( slot.MinX, slot.MinY, slot.MaxX, slot.MaxY ) < slot.Threshold )
				continue;

			slot.Fired = true;
			GameCore.Instance?.NotifyDiscovery( slot.Id, slot.Monologue );
		}
	}

	private float RegionRevealProgress( int minX, int minY, int maxX, int maxY )
	{
		var total = 0;
		var revealed = 0;

		for ( var y = minY; y <= maxY; y++ )
		for ( var x = minX; x <= maxX; x++ )
		{
			var idx = y * _texW + x;
			if ( !_active[idx] ) continue;
			total++;

			var clear = true;
			for ( var i = 0; i < _layers.Count; i++ )
			{
				if ( _layers[i].Clean[idx] < 1f )
				{
					clear = false;
					break;
				}
			}

			if ( clear )
				revealed++;
		}

		return total == 0 ? 0f : (float)revealed / total;
	}

	private int LayerIndex( ToolType tool )
	{
		for ( var i = 0; i < _layers.Count; i++ )
			if ( _layers[i].Tool == tool )
				return i;
		return -1;
	}

	/// <summary>True when every layer beneath <paramref name="layerIdx"/> is fully clear at
	/// <paramref name="idx"/>.</summary>
	private bool PriorLayersClean( int layerIdx, int idx )
	{
		for ( var i = 0; i < layerIdx; i++ )
			if ( _layers[i].Clean[idx] < 1f )
				return false;
		return true;
	}

	/// <summary>Whether any texel on this layer can still be cleaned with its tool.</summary>
	private bool LayerHasWork( int layerIdx )
	{
		var layer = _layers[layerIdx];
		if ( layer.Cleaned >= layer.Total )
			return false;

		// Fast path: the base grime layer is always fair game until it's fully done.
		if ( layerIdx == 0 )
			return true;

		// Follow-up layers: work exists wherever the prior pass is already clear locally.
		var texelCount = layer.Clean.Length;
		for ( var i = 0; i < texelCount; i++ )
		{
			if ( _active is not null && !_active[i] ) continue;
			if ( layer.Clean[i] >= 1f ) continue;
			if ( PriorLayersClean( layerIdx, i ) )
				return true;
		}

		return false;
	}

	/// <summary>Map a world point + radius to the affected texel rectangle. False if off-panel.</summary>
	private bool Region( Vector3 worldPoint, float radius,
		out float fx, out float fy, out float rx, out float ry,
		out int minX, out int minY, out int maxX, out int maxY )
	{
		var local = WorldTransform.PointToLocal( worldPoint );
		fx = (local.x / Width + 0.5f) * _texW;
		fy = (local.y / Height + 0.5f) * _texH;
		rx = Math.Max( 0.75f, radius / Width * _texW );
		ry = Math.Max( 0.75f, radius / Height * _texH );

		minX = Math.Max( 0, (int)MathF.Floor( fx - rx ) );
		maxX = Math.Min( _texW - 1, (int)MathF.Ceiling( fx + rx ) );
		minY = Math.Max( 0, (int)MathF.Floor( fy - ry ) );
		maxY = Math.Min( _texH - 1, (int)MathF.Ceiling( fy + ry ) );

		return minX <= maxX && minY <= maxY;
	}

	/// <summary>Push a changed sub-rectangle of a layer's mask to its GPU texture.</summary>
	private void UploadRegion( Layer layer, int x0, int y0, int x1, int y1 )
	{
		if ( layer.Texture is null ) return;

		var w = x1 - x0 + 1;
		var h = y1 - y0 + 1;
		var n = w * h;
		if ( layer.Scratch is null || layer.Scratch.Length != n )
			layer.Scratch = new Color32[n];

		var k = 0;
		for ( var y = y0; y <= y1; y++ )
		{
			var rowBase = y * _texW + x0;
			for ( var x = 0; x < w; x++ )
				layer.Scratch[k++] = layer.Pixels[rowBase + x];
		}

		layer.Texture.Update( layer.Scratch, x0, y0, w, h );
	}
}
