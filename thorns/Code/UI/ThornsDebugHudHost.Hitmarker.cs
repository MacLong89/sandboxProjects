#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Center-screen hitmarker: server-confirmed hits only (<see cref="ThornsWeapon.RpcFireOutcome"/> → <see cref="NotifyLocalWeaponHitFeedback"/>).
/// Persistent UI layer (not rebuilt with the rest of the HUD) for smooth animation.
/// </summary>
public sealed partial class ThornsDebugHudHost
{
	Panel _hitMarkerHost;
	Panel _hitMarkerAlignWrapper;
	Panel _hitMarkerPivot;
	Panel _hitMarkerArmTop;
	Panel _hitMarkerArmBottom;
	Panel _hitMarkerArmLeft;
	Panel _hitMarkerArmRight;

	/// <summary>Real time when the current pulse began (server-confirmed hit).</summary>
	double _hitPulseStart;

	/// <summary>Latest damage in the current blend window (scales marker size / brightness).</summary>
	float _hitPulseDamage;

	bool _hitPulseAccent;
	bool _hitMarkerHostBuilt;

	/// <summary>Rapid-fire: extend visibility without restarting pop from zero every frame.</summary>
	double _hitBlendUntil;

	float _crosshairBump = 1f;

	// --- Tuning (inspector) ---

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerPopSeconds { get; set; } = 0.05f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerSnapSeconds { get; set; } = 0.03f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerFadeSeconds { get; set; } = 0.13f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerScaleMin { get; set; } = 0.82f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerScalePeak { get; set; } = 1.22f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerDamageScaleMul { get; set; } = 0.35f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerHeadshotScaleMul { get; set; } = 1.18f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerBlendWindowSeconds { get; set; } = 0.05f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float CrosshairBumpOnHit { get; set; } = 1.09f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float CrosshairBumpDecay { get; set; } = 14f;

	/// <summary>Optional pixel nudge after centering; negative X shifts left if the gap-X sits right of the reticle.</summary>
	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerCenterOffsetXPixels { get; set; } = -4f;

	[Property, Category( "Thorns/UI/Hitmarker" )] public float HitMarkerCenterOffsetYPixels { get; set; } = 0f;

	/// <summary>Matches <c>BuildCrosshair</c> — only used when the shell HUD is not driving the reticle.</summary>
	static void GetDebugHudCrosshairStrokePx( float bumpClamped, out float thickPx, out float spanPx, out float gapPx )
	{
		thickPx = Math.Max( 1f, MathF.Round( 2f * bumpClamped ) );
		spanPx = Math.Max( 1f, MathF.Round( 5f * bumpClamped ) );
		gapPx = Math.Max( 1f, MathF.Round( 2f * bumpClamped ) );
	}

	/// <summary>Bounding box of the + reticle — same as <c>BuildCrosshair</c> stack dimensions.</summary>
	static void GetCrosshairStackBoxPx( float thickPx, float spanPx, float gapPx, out float stackW, out float stackH )
	{
		var midRowH = Math.Max( thickPx, gapPx );
		stackW = 2f * spanPx + gapPx;
		stackH = 2f * spanPx + midRowH;
	}

	void GetCrosshairStrokeMetricsForHitmarker( out float thickPx, out float spanPx, out float gapPx )
	{
		var shell = Components.Get<ThornsGameShell>( FindMode.EnabledInSelf );
		if ( shell is { IsValid: true, Enabled: true } )
		{
			thickPx = 2f;
			spanPx = 14f;
			gapPx = 4f;
			return;
		}

		GetDebugHudCrosshairStrokePx( Math.Clamp( _crosshairBump, 1f, 1.35f ), out thickPx, out spanPx, out gapPx );
	}

	void EnsureHitMarkerHost()
	{
		if ( _hitMarkerHostBuilt && _hitMarkerHost.IsValid )
			return;

		if ( Panel is null || !Panel.IsValid )
			return;

		_hitMarkerHost = ThornsUiPanelAdd.AddChildPanel(Panel,  "thorns-hit-marker-host" );
		_hitMarkerHost.Style.Position = PositionMode.Absolute;
		_hitMarkerHost.Style.Left = 0;
		_hitMarkerHost.Style.Top = 0;
		_hitMarkerHost.Style.Width = Length.Fraction( 1f );
		_hitMarkerHost.Style.Height = Length.Fraction( 1f );
		_hitMarkerHost.Style.PointerEvents = PointerEvents.None;
		_hitMarkerHost.Style.ZIndex = 92;

		var hitLayer = ThornsUiPanelAdd.AddChildPanel(_hitMarkerHost,  "thorns-hit-marker-layer" );
		hitLayer.Style.Position = PositionMode.Absolute;
		hitLayer.Style.Left = 0;
		hitLayer.Style.Top = 0;
		hitLayer.Style.Width = Length.Fraction( 1f );
		hitLayer.Style.Height = Length.Fraction( 1f );

		_hitMarkerAlignWrapper = ThornsUiPanelAdd.AddChildPanel(hitLayer,  "thorns-hit-marker-crosshair-align" );
		_hitMarkerAlignWrapper.Style.Position = PositionMode.Absolute;
		_hitMarkerAlignWrapper.Style.Left = Length.Fraction( 0.5f );
		_hitMarkerAlignWrapper.Style.Top = Length.Fraction( 0.5f );

		_hitMarkerPivot = ThornsUiPanelAdd.AddChildPanel(_hitMarkerAlignWrapper,  "thorns-hit-gap-cross-pivot" );
		_hitMarkerPivot.Style.Position = PositionMode.Absolute;
		_hitMarkerPivot.Style.Left = Length.Fraction( 0.5f );
		_hitMarkerPivot.Style.Top = Length.Fraction( 0.5f );
		_hitMarkerPivot.Style.Width = Length.Pixels( 2 );
		_hitMarkerPivot.Style.Height = Length.Pixels( 2 );
		_hitMarkerPivot.Style.MarginLeft = Length.Pixels( -1 );
		_hitMarkerPivot.Style.MarginTop = Length.Pixels( -1 );

		_hitMarkerArmTop = ThornsUiPanelAdd.AddChildPanel(_hitMarkerPivot,  "thorns-hit-gap-arm thorns-hit-gap-arm--diag-nw" );
		_hitMarkerArmBottom = ThornsUiPanelAdd.AddChildPanel(_hitMarkerPivot,  "thorns-hit-gap-arm thorns-hit-gap-arm--diag-se" );
		_hitMarkerArmLeft = ThornsUiPanelAdd.AddChildPanel(_hitMarkerPivot,  "thorns-hit-gap-arm thorns-hit-gap-arm--diag-ne" );
		_hitMarkerArmRight = ThornsUiPanelAdd.AddChildPanel(_hitMarkerPivot,  "thorns-hit-gap-arm thorns-hit-gap-arm--diag-sw" );

		SetHitMarkerInvisible();
		_hitMarkerHostBuilt = true;
	}

	void SetHitMarkerInvisible()
	{
		if ( !_hitMarkerAlignWrapper.IsValid || !_hitMarkerPivot.IsValid
		     || !_hitMarkerArmTop.IsValid || !_hitMarkerArmBottom.IsValid
		     || !_hitMarkerArmLeft.IsValid || !_hitMarkerArmRight.IsValid )
			return;

		_hitMarkerPivot.Style.Opacity = 0f;
		_hitMarkerPivot.Style.MarginLeft = Length.Pixels( -1 );
		_hitMarkerPivot.Style.MarginTop = Length.Pixels( -1 );

		var z = new Color( 1f, 0.2f, 0.2f, 0f );
		_hitMarkerArmTop.Style.BackgroundColor = z;
		_hitMarkerArmBottom.Style.BackgroundColor = z;
		_hitMarkerArmLeft.Style.BackgroundColor = z;
		_hitMarkerArmRight.Style.BackgroundColor = z;

		_hitMarkerArmTop.Style.Transform = null;
		_hitMarkerArmBottom.Style.Transform = null;
		_hitMarkerArmLeft.Style.Transform = null;
		_hitMarkerArmRight.Style.Transform = null;
		_hitMarkerArmTop.Style.TransformOriginX = null;
		_hitMarkerArmTop.Style.TransformOriginY = null;
		_hitMarkerArmBottom.Style.TransformOriginX = null;
		_hitMarkerArmBottom.Style.TransformOriginY = null;
		_hitMarkerArmLeft.Style.TransformOriginX = null;
		_hitMarkerArmLeft.Style.TransformOriginY = null;
		_hitMarkerArmRight.Style.TransformOriginX = null;
		_hitMarkerArmRight.Style.TransformOriginY = null;
	}

	/// <summary>Authoritative hit feedback (owner client). Works for players, NPCs, wildlife — any living target with damage dealt.</summary>
	public void NotifyLocalWeaponHitFeedback( float damageDealt, bool hitMarkerHighlight, bool killingBlow )
	{
		var now = Time.Now;
		if ( now <= _hitBlendUntil )
		{
			_hitPulseDamage = Math.Max( _hitPulseDamage, damageDealt );
			_hitPulseAccent |= hitMarkerHighlight;
		}
		else
		{
			_hitPulseStart = now;
			_hitPulseDamage = damageDealt;
			_hitPulseAccent = hitMarkerHighlight;
		}

		_hitBlendUntil = now + HitMarkerBlendWindowSeconds;
		_crosshairBump = Math.Max( _crosshairBump, CrosshairBumpOnHit );

		RequestHudRebuild();
	}

	void TickHitMarkerPresentation()
	{
		if ( !_hitMarkerHost.IsValid || !_hitMarkerAlignWrapper.IsValid || !_hitMarkerPivot.IsValid
		     || !_hitMarkerArmTop.IsValid || !_hitMarkerArmBottom.IsValid
		     || !_hitMarkerArmLeft.IsValid || !_hitMarkerArmRight.IsValid )
			return;

		var now = Time.Now;
		var totalDur = HitMarkerPopSeconds + HitMarkerSnapSeconds + HitMarkerFadeSeconds;
		var elapsed = now - _hitPulseStart;
		if ( elapsed > totalDur )
		{
			_hitPulseDamage = 0f;
			SetHitMarkerInvisible();
			return;
		}

		if ( elapsed < 0.0 || _hitPulseDamage <= 0.01f )
		{
			SetHitMarkerInvisible();
			return;
		}

		var dmgNorm = Math.Clamp( _hitPulseDamage / 120f, 0f, 1.5f );
		var dmgBoost = 1f + dmgNorm * HitMarkerDamageScaleMul;
		var hsBoost = _hitPulseAccent ? HitMarkerHeadshotScaleMul : 1f;

		float scale;
		if ( elapsed <= HitMarkerPopSeconds )
		{
			var u = (float)(elapsed / HitMarkerPopSeconds);
			var t = 1f - MathF.Pow( 1f - u, 2.5f );
			scale = HitMarkerScaleMin + (HitMarkerScalePeak - HitMarkerScaleMin) * t;
		}
		else if ( elapsed <= HitMarkerPopSeconds + HitMarkerSnapSeconds )
		{
			var u = (float)((elapsed - HitMarkerPopSeconds) / HitMarkerSnapSeconds);
			scale = HitMarkerScalePeak + (1f - HitMarkerScalePeak) * Math.Clamp( u, 0f, 1f );
		}
		else
		{
			scale = 1f;
		}

		scale *= dmgBoost * hsBoost;

		var fadeT = (float)((elapsed - HitMarkerPopSeconds - HitMarkerSnapSeconds) / HitMarkerFadeSeconds);
		fadeT = Math.Clamp( fadeT, 0f, 1f );
		var alpha = 1f - fadeT * fadeT;

		GetCrosshairStrokeMetricsForHitmarker( out var cxThick, out var cxSpan, out var cxGap );
		GetCrosshairStackBoxPx( cxThick, cxSpan, cxGap, out var stackW, out var stackH );
		// Floor negative half-sizes so odd stack widths center consistently (Round(-n.5) can bias right).
		_hitMarkerAlignWrapper.Style.MarginLeft = Length.Pixels( MathF.Floor( -stackW * 0.5f + 1e-4f ) );
		_hitMarkerAlignWrapper.Style.MarginTop = Length.Pixels( MathF.Floor( -stackH * 0.5f + 1e-4f ) );
		_hitMarkerAlignWrapper.Style.Width = Length.Pixels( stackW );
		_hitMarkerAlignWrapper.Style.Height = Length.Pixels( stackH );

		var hsMul = _hitPulseAccent ? 1.12f : 1f;
		var thickPx = Math.Max( 1f, MathF.Round( cxThick * scale * hsMul ) );
		var armLenPx = Math.Max( 1f, MathF.Round( cxSpan * scale * 0.92f ) );
		var gapHalfPx = cxGap * scale * 0.5f;

		var ink = _hitPulseAccent
			? new Color( 1f, 0.22f, 0.22f, 0.97f * alpha )
			: new Color( 1f, 1f, 1f, 0.96f * alpha );

		_hitMarkerPivot.Style.Opacity = 1f;
		_hitMarkerPivot.Style.MarginLeft = Length.Pixels( MathF.Round( -1f + HitMarkerCenterOffsetXPixels ) );
		_hitMarkerPivot.Style.MarginTop = Length.Pixels( MathF.Round( -1f + HitMarkerCenterOffsetYPixels ) );

		// Origin = crosshair center (pivot is 2×2 at 50%/50% with −1px margins). Inner void tracks reticle gap; arms track span.
		const float invSqrt2 = 0.70710678118f;
		var o = (gapHalfPx + armLenPx * 0.5f) * invSqrt2;

		void ApplyGapDiagonalArm( Panel bar, float ox, float oy, float rotationZDegrees )
		{
			bar.Style.Position = PositionMode.Absolute;
			bar.Style.Left = Length.Fraction( 0.5f );
			bar.Style.Top = Length.Fraction( 0.5f );
			bar.Style.Width = Length.Pixels( armLenPx );
			bar.Style.Height = Length.Pixels( thickPx );
			bar.Style.MarginLeft = Length.Pixels( MathF.Round( ox - armLenPx * 0.5f ) );
			bar.Style.MarginTop = Length.Pixels( MathF.Round( oy - thickPx * 0.5f ) );
			bar.Style.BackgroundColor = ink;

			var xf = new PanelTransform();
			xf.AddRotation( 0f, 0f, rotationZDegrees );
			bar.Style.Transform = xf;
			bar.Style.TransformOriginX = Length.Fraction( 0.5f );
			bar.Style.TransformOriginY = Length.Fraction( 0.5f );
		}

		ApplyGapDiagonalArm( _hitMarkerArmTop, -o, -o, 45f );
		ApplyGapDiagonalArm( _hitMarkerArmBottom, o, o, 45f );
		ApplyGapDiagonalArm( _hitMarkerArmLeft, o, -o, -45f );
		ApplyGapDiagonalArm( _hitMarkerArmRight, -o, o, -45f );
	}

	void TickCrosshairBumpDecay()
	{
		if ( _crosshairBump > 1.001f )
			_crosshairBump = Math.Max( 1f, MathX.Lerp( _crosshairBump, 1f, CrosshairBumpDecay * Time.Delta ) );
	}

	/// <summary>Preserves the hitmarker layer across full HUD rebuilds.</summary>
	bool IsHitMarkerHostPanel( Panel p ) => _hitMarkerHost.IsValid && p == _hitMarkerHost;
}
