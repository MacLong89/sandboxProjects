namespace Sandbox;

using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;

/// <summary>
/// Local-owner ADS sight mode, presentation blend, classic sniper scope timing, and target FOV.
/// Drives <see cref="ThornsViewModelController"/> sight presentation and <see cref="Terraingen.UI.Hud.ThornsSniperScopeHudState"/>.
/// </summary>
[Title( "Thorns — ADS Sight Controller" )]
[Category( "Thorns" )]
[Icon( "center_focus_strong" )]
[Order( 104 )]
public sealed class ThornsAdsSightController : Component
{
	ThornsViewModelController _viewModel;
	ThornsFpPresentation _fp;
	ThornsPawnCamera _pawnCamera;

	ThornsAdsSightMode _adsSightMode = ThornsAdsSightMode.None;
	float _adsPresentationBlend;
	TimeSince _adsHeld;
	bool _wasAds;
	bool _classicScopeOverlayArmed;
	TimeSince _classicScopeOverlayHold;
	bool _classicScopeEngaged;

	List<ThornsAttachmentId> _lastAttachments = [];
	string _lastCombatId = "";

	public ThornsAdsSightMode AdsSightMode => _adsSightMode;
	public float AdsPresentationBlend => _adsPresentationBlend;

	public bool HideStandardCrosshair =>
		WantsAds
		&& _adsPresentationBlend >= ThornsAdsSightTuning.HideCrosshairBlend
		&& _adsSightMode != ThornsAdsSightMode.SniperScope
		&& !ShowHoloSightCenterDot;

	/// <summary>Keep the HUD center dot visible while ADS with a holographic sight (aimbox parity).</summary>
	public bool ShowHoloSightCenterDot =>
		WantsAds
		&& _adsPresentationBlend >= ThornsAdsSightTuning.HideCrosshairBlend
		&& _adsSightMode == ThornsAdsSightMode.RedDot
		&& HasEquippedAttachment( ThornsAttachmentId.HoloSight );

	public bool ShowClassicSniperScope => IsClassicSniperScopeActive();

	public bool UseScopedLookSensitivity => ShowClassicSniperScope;

	public bool UseRedDotLookSensitivity =>
		WantsAds
		&& _adsPresentationBlend >= ThornsAdsSightTuning.HideCrosshairBlend
		&& _adsSightMode == ThornsAdsSightMode.RedDot;

	bool WantsAds =>
		(Input.Down( "Attack2" ) || Input.Down( "attack2" ))
		&& AllowsAdsPresentation();

	protected override void OnAwake()
	{
		_viewModel = Components.Get<ThornsViewModelController>();
		var parent = GameObject.Parent;
		if ( parent.IsValid() )
			_fp = parent.Components.Get<ThornsFpPresentation>();
		_pawnCamera = Components.Get<ThornsPawnCamera>();
	}

	protected override void OnUpdate()
	{
		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) || !Game.IsPlaying )
			return;

		if ( _viewModel is null || !_viewModel.IsValid() )
			_viewModel = Components.Get<ThornsViewModelController>();
		if ( _fp is null || !_fp.IsValid() )
		{
			var parent = GameObject.Parent;
			_fp = parent.IsValid() ? parent.Components.Get<ThornsFpPresentation>() : default;
		}

		TickAttachmentSync();
		TickAdsSightState();
		PublishHudState();
	}

	void TickAttachmentSync()
	{
		if ( _viewModel is null || !_viewModel.IsValid() || !_viewModel.HasActiveViewModel )
			return;

		if ( !TryResolveOwnerWeaponContext( out var combatId, out var attachments, out _ ) )
			return;

		if ( combatId == _lastCombatId && AttachmentListsEqual( _lastAttachments, attachments ) )
			return;

		_viewModel.SyncAttachments( combatId, attachments );
		_lastCombatId = combatId;
		_lastAttachments = attachments.ToList();
	}

	void TickAdsSightState()
	{
		_adsSightMode = ResolveAdsSightMode();

		if ( WantsAds && _adsSightMode != ThornsAdsSightMode.None )
		{
			if ( !_wasAds )
				_adsHeld = 0;

			var animBlend = _viewModel?.AdsBlend01 ?? 0f;
			var fallbackBlend = Math.Clamp( _adsHeld / 0.35f, 0f, 1f );
			_adsPresentationBlend = animBlend > 0.01f ? animBlend : fallbackBlend;
		}
		else
		{
			_adsPresentationBlend = 0f;
			_classicScopeEngaged = false;
		}

		_wasAds = WantsAds && _adsSightMode != ThornsAdsSightMode.None;
		TickClassicScopeEngagement();
		TickClassicScopeOverlayDelay();
		_viewModel?.ApplySightPresentation( _adsSightMode, _adsPresentationBlend, ShowClassicSniperScope );
	}

	void PublishHudState()
	{
		Terraingen.UI.Hud.ThornsSniperScopeHudState.ShowClassicScope = ShowClassicSniperScope;
		Terraingen.UI.Hud.ThornsSniperScopeHudState.HideStandardCrosshair = HideStandardCrosshair;
		Terraingen.UI.Hud.ThornsSniperScopeHudState.ShowScopeCrosshair =
			ShowClassicSniperScope && !HideStandardCrosshair;
		Terraingen.UI.Hud.ThornsSniperScopeHudState.HideGameplayHotbar = ShouldHideGameplayHotbar();
	}

	bool ShouldHideGameplayHotbar() =>
		WantsAds
		&& _adsPresentationBlend >= ThornsAdsSightTuning.HideCrosshairBlend
		&& _adsSightMode == ThornsAdsSightMode.SniperScope;

	bool AllowsAdsPresentation()
	{
		if ( _fp is null || !_fp.IsValid() )
			return true;

		var combatId = _fp.ClientMirrorCombatDefinitionId ?? "";
		if ( ThornsFpToolCombat.TreatsAsMeleeWeapon( combatId ) )
			return false;

		return _fp.ClientMirrorFpPresentationAllowsCombatLayers();
	}

	ThornsAdsSightMode ResolveAdsSightMode()
	{
		if ( !WantsAds || !TryResolveOwnerWeaponContext( out var combatId, out var attachments, out var effective ) )
			return ThornsAdsSightMode.None;

		if ( ThornsAttachmentCatalog.UsesIntegratedSniperScope( combatId ) )
		{
			return attachments.Contains( ThornsAttachmentId.RangedSight )
				? ThornsAdsSightMode.SniperScope
				: ThornsAdsSightMode.IronSight;
		}

		return effective.AdsSightMode;
	}

	bool TryResolveOwnerWeaponContext(
		out string combatId,
		out IReadOnlyList<ThornsAttachmentId> attachments,
		out ThornsWeaponEffectiveStats effective )
	{
		combatId = "";
		attachments = Array.Empty<ThornsAttachmentId>();
		effective = default;

		var pawnRoot = GameObject.Parent;
		if ( !pawnRoot.IsValid() )
			return false;

		var gameplay = pawnRoot.Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarItemId( out var itemId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var idef ) || idef.ItemType != ThornsItemType.Weapon )
			return false;

		combatId = ThornsInventoryWeaponState.ResolveCombatId( idef, itemId );
		var def = ThornsWeaponDefinitions.Get( combatId );
		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( def, combatId ) || ThornsWeaponDefinitions.IsBowWeapon( def, combatId ) )
			return false;

		if ( !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return false;

		var stack = gameplay.GetHotbarSlot( hotbar );
		attachments = ThornsWeaponAttachmentState.GetAttachments( stack );
		effective = ThornsWeaponEffectiveStats.Resolve( def, combatId, attachments );
		return true;
	}

	bool IsClassicSniperScopeEligible() =>
		WantsAds
		&& _adsSightMode == ThornsAdsSightMode.SniperScope
		&& _classicScopeEngaged;

	void TickClassicScopeEngagement()
	{
		if ( !WantsAds || _adsSightMode != ThornsAdsSightMode.SniperScope )
		{
			_classicScopeEngaged = false;
			return;
		}

		if ( _classicScopeEngaged )
		{
			if ( _adsPresentationBlend < ThornsAdsSightTuning.SniperClassicScopeExitBlend )
				_classicScopeEngaged = false;
		}
		else if ( _adsPresentationBlend >= ThornsAdsSightTuning.SniperClassicScopeEnterBlend )
		{
			_classicScopeEngaged = true;
		}
	}

	bool IsClassicSniperScopeActive() =>
		IsClassicSniperScopeEligible()
		&& _classicScopeOverlayArmed
		&& _classicScopeOverlayHold >= ThornsAdsSightTuning.SniperClassicScopeOverlayDelaySeconds;

	void TickClassicScopeOverlayDelay()
	{
		if ( IsClassicSniperScopeEligible() )
		{
			if ( !_classicScopeOverlayArmed )
			{
				_classicScopeOverlayArmed = true;
				_classicScopeOverlayHold = 0f;
			}

			return;
		}

		_classicScopeOverlayArmed = false;
	}

	public float ResolveTargetFieldOfView( float hipFieldOfView, IReadOnlyList<ThornsAttachmentId> attachments )
	{
		if ( !WantsAds || _adsSightMode == ThornsAdsSightMode.None )
			return hipFieldOfView;

		if ( _adsSightMode == ThornsAdsSightMode.SniperScope )
		{
			if ( !IsClassicSniperScopeActive() )
				return hipFieldOfView;

			var scopeT = Math.Clamp(
				( _adsPresentationBlend - ThornsAdsSightTuning.SniperClassicScopeEnterBlend )
				/ ( 1f - ThornsAdsSightTuning.SniperClassicScopeEnterBlend ),
				0f,
				1f );
			return MathX.Lerp(
				hipFieldOfView,
				ThornsAdsSightTuning.SniperScopeViewFov,
				scopeT );
		}

		var scopedFov = ThornsAdsSightTuning.ResolveAdsFieldOfView( _adsSightMode, attachments );
		return MathX.Lerp( hipFieldOfView, scopedFov, _adsPresentationBlend );
	}

	public void ApplyLookSensitivityScale( ref float lookSensitivity )
	{
		if ( UseScopedLookSensitivity )
			lookSensitivity *= ThornsAdsSightTuning.SniperLookMultiplier;
		else if ( UseRedDotLookSensitivity )
			lookSensitivity *= ThornsAdsSightTuning.RedDotLookMultiplier;
	}

	static bool AttachmentListsEqual( IReadOnlyList<ThornsAttachmentId> a, IReadOnlyList<ThornsAttachmentId> b )
	{
		if ( ReferenceEquals( a, b ) )
			return true;

		if ( a is null || b is null || a.Count != b.Count )
			return false;

		for ( var i = 0; i < a.Count; i++ )
		{
			if ( a[i] != b[i] )
				return false;
		}

		return true;
	}

	bool HasEquippedAttachment( ThornsAttachmentId attachment )
	{
		if ( !TryResolveOwnerWeaponContext( out _, out var attachments, out _ ) )
			return false;

		return attachments.Contains( attachment );
	}
}
