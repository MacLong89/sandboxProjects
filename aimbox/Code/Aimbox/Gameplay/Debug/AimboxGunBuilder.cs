using System.Text;

namespace Sandbox;

/// <summary>
/// Inspector-driven attachment lab — pick a weapon, toggle attachments, preview on the FP viewmodel.
/// Use with the <c>gun_builder</c> scene (Aimbox Game → Gun Builder Scene enabled).
/// </summary>
[Title( "Aimbox Gun Builder" )]
[Category( "Aimbox/Debug" )]
[Icon( "hardware" )]
public sealed class AimboxGunBuilder : Component
{
	public static AimboxGunBuilder Instance { get; private set; }

	[Property, Group( "Weapon" ), Title( "Preview weapon" )]
	public AimboxWeaponId Weapon { get; set; }

	[Property, Group( "Attachments — Sights" )] public bool HoloSight { get; set; }
	[Property, Group( "Attachments — Sights" )] public bool RaisedRedDot { get; set; }
	[Property, Group( "Attachments — Sights" )] public bool RangedSight { get; set; }

	[Property, Group( "Attachments — Other" )] public bool ExtendedMag { get; set; }
	[Property, Group( "Attachments — Other" )] public bool ForegripStraight { get; set; }
	[Property, Group( "Attachments — Other" )] public bool ForegripAngled { get; set; }
	[Property, Group( "Attachments — Other" )] public bool Flashlight { get; set; }
	[Property, Group( "Attachments — Other" )] public bool Suppressor { get; set; }

	[Property, Group( "Debug" ), Title( "Log VM attachment mounts to console" )]
	public bool VerboseMountLog { get; set; } = true;

	[Property, Group( "Viewmodel — M700 scope ADS" )]
	[Title( "Apply M700 scope ADS viewmodel offset (hold RMB to preview)" )]
	public bool ApplyM700ScopeAdsFineTune { get; set; } = true;

	[Property, Group( "Viewmodel — M700 scope ADS" )]
	[Title( "Viewmodel nudge (+Z up on screen, −Z down). Requires Ranged Sight ON." )]
	public Vector3 M700ScopeAdsViewmodelFineTune { get; set; }

	[Property, ReadOnly, Group( "Status" )] public string PlayerStatus { get; private set; } = "Waiting for player…";
	[Property, ReadOnly, Group( "Status" )] public string EquippedSummary { get; private set; } = "";
	[Property, ReadOnly, Group( "Status" ), Title( "Catalog allowlist + mesh availability" )]
	public string CompatibilityReport { get; private set; } = "";

	int _revision = -1;
	int _lastDroppedLogRevision = -1;

	public void ForceReapply()
	{
		AimboxAttachmentPipelineDebug.Reg( $"GunBuilder.ForceReapply weapon={Weapon} toggles={DescribeToggles()}" );
		_revision = -1;
		TryApply();
	}

	protected override void OnStart()
	{
		Instance = this;
		M700ScopeAdsViewmodelFineTune = AimboxAdsSightTuning.M700ScopeAdsViewmodelFineTune;
		SyncMountLogging();
		TryApply();
	}

	protected override void OnEnabled()
	{
		Instance = this;
		SyncMountLogging();
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		SyncMountLogging();

		var revision = ComputeRevision();
		if ( revision == _revision && IsAppliedToPlayer() )
		{
			UpdateStatus( FindLocalPlayer( Scene ) );
			return;
		}

		var player = FindLocalPlayer( Scene );
		if ( player is not null && AimboxAttachmentCatalog.NormalizeWeapon( player.ActiveWeapon )
		     != AimboxAttachmentCatalog.NormalizeWeapon( Weapon ) )
		{
			AimboxAttachmentPipelineDebug.Reg(
				$"GunBuilder revision={revision} inspectorWeapon={Weapon} activeWeapon={player.ActiveWeapon} — will apply inspector weapon." );
		}

		_revision = revision;
		TryApply();
	}

	void SyncMountLogging() => AimboxViewModelAttachmentDebug.Enabled = VerboseMountLog;

	void TryApply()
	{
		var player = FindLocalPlayer( Scene );
		if ( player is null )
		{
			PlayerStatus = "Waiting for local player…";
			AimboxAttachmentPipelineDebug.Reg( "GunBuilder.TryApply waiting for local player." );
			return;
		}

		if ( !AimboxAttachmentCatalog.SupportsAttachments( Weapon ) )
		{
			PlayerStatus = $"{Weapon} has no attachment support — pick M4, MP5, USP, M700, or Spaghelli M4.";
			AimboxAttachmentPipelineDebug.Reg( $"GunBuilder.TryApply aborted — {Weapon} has no attachment support." );
			return;
		}

		var weapon = AimboxAttachmentCatalog.NormalizeWeapon( Weapon );
		var applied = BuildAppliedAttachments();
		AimboxAttachmentPipelineDebug.Reg(
			$"GunBuilder.TryApply weapon={weapon} toggles={DescribeToggles()} applied=[{AimboxAttachmentPipelineDebug.FormatList( applied )}] activeBefore={player.ActiveWeapon} runtimeBefore=[{AimboxAttachmentPipelineDebug.FormatList( player.CurrentWeapon?.Attachments )}]" );
		player.ApplyDebugWeaponLoadout( weapon, applied );
		UpdateStatus( player );
	}

	bool IsAppliedToPlayer()
	{
		var player = FindLocalPlayer( Scene );
		var weapon = AimboxAttachmentCatalog.NormalizeWeapon( Weapon );
		if ( player is null || !AimboxAttachmentCatalog.SupportsAttachments( weapon ) )
			return false;

		var runtime = player.CurrentWeapon;
		if ( runtime is null || AimboxAttachmentCatalog.NormalizeWeapon( runtime.Definition.Id ) != weapon )
			return false;

		return AttachmentSetsEqual( runtime.Attachments, BuildAppliedAttachments() );
	}

	static bool AttachmentSetsEqual( IReadOnlyCollection<AimboxAttachmentId> a, IReadOnlyCollection<AimboxAttachmentId> b ) =>
		a.Count == b.Count && new HashSet<AimboxAttachmentId>( a ).SetEquals( b );

	List<AimboxAttachmentId> BuildAppliedAttachments()
	{
		var weapon = AimboxAttachmentCatalog.NormalizeWeapon( Weapon );
		var toggled = new List<AimboxAttachmentId>();
		TryAddToggle( toggled, AimboxAttachmentId.HoloSight, HoloSight );
		TryAddToggle( toggled, AimboxAttachmentId.RaisedRedDot, RaisedRedDot );
		TryAddToggle( toggled, AimboxAttachmentId.RangedSight, RangedSight );
		TryAddToggle( toggled, AimboxAttachmentId.ExtendedMag, ExtendedMag );
		TryAddToggle( toggled, AimboxAttachmentId.ForegripStraight, ForegripStraight );
		TryAddToggle( toggled, AimboxAttachmentId.ForegripAngled, ForegripAngled );
		TryAddToggle( toggled, AimboxAttachmentId.Flashlight, Flashlight );
		TryAddToggle( toggled, AimboxAttachmentId.Suppressor, Suppressor );

		var compatible = toggled
			.Where( a => AimboxAttachmentCatalog.IsCompatible( weapon, a ) )
			.ToList();

		if ( compatible.Count != toggled.Count && _lastDroppedLogRevision != _revision )
		{
			var dropped = toggled.Except( compatible ).ToList();
			AimboxAttachmentPipelineDebug.Reg(
				$"GunBuilder.BuildAppliedAttachments dropped incompatible on {weapon}: [{AimboxAttachmentPipelineDebug.FormatList( dropped )}] allow=[{AimboxAttachmentPipelineDebug.FormatList( AimboxAttachmentCatalog.GetCompatibleAttachments( weapon ) )}]" );
			_lastDroppedLogRevision = _revision;
		}

		var result = AimboxAttachmentCatalog.EnforceExclusivity( compatible );
		if ( result.Count != compatible.Count && _lastDroppedLogRevision != _revision )
		{
			AimboxAttachmentPipelineDebug.Reg(
				$"GunBuilder.BuildAppliedAttachments exclusivity trimmed [{AimboxAttachmentPipelineDebug.FormatList( compatible )}] -> [{AimboxAttachmentPipelineDebug.FormatList( result )}]" );
			_lastDroppedLogRevision = _revision;
		}

		return result;
	}

	static void TryAddToggle( List<AimboxAttachmentId> list, AimboxAttachmentId id, bool enabled )
	{
		if ( enabled )
			list.Add( id );
	}

	void UpdateStatus( AimboxPlayerController player )
	{
		if ( player is null )
		{
			PlayerStatus = "Waiting for local player…";
			return;
		}

		var applied = BuildAppliedAttachments();
		var summary = applied.Count <= 0
			? "(none)"
			: string.Join( ", ", applied.Select( AimboxAttachmentCatalog.Label ) );

		if ( EquippedSummary != summary )
			EquippedSummary = summary;

		var status =
			$"Equipped {AimboxWeapons.Get( Weapon ).Name} — hold RMB to ADS. Toggle attachments above; exclusivity keeps one sight / one foregrip.";
		if ( Weapon == AimboxWeaponId.M700 )
			status += " M700 scope ADS: enable Ranged Sight, then edit Viewmodel — M700 scope ADS while playing.";
		if ( PlayerStatus != status )
			PlayerStatus = status;

		var report = new StringBuilder();
		var weapon = AimboxAttachmentCatalog.NormalizeWeapon( Weapon );
		foreach ( var attachment in AimboxAttachmentCatalog.StandardAttachments )
		{
			var label = AimboxAttachmentCatalog.Label( attachment );
			var on = IsToggleOn( attachment );
			var allow = AimboxAttachmentCatalog.IsCompatible( weapon, attachment );
			var visual = AimboxSboxAttachmentCatalog.HasVisual( weapon, attachment );
			var appliedNow = applied.Contains( attachment );

			report.Append( label );
			report.Append( on ? " [ON]" : " [off]" );
			report.Append( allow ? " allow" : " NO-allow" );
			report.Append( visual ? " mesh" : allow && weapon == AimboxWeaponId.M700 && attachment == AimboxAttachmentId.RangedSight ? " integrated" : " no-mesh" );
			report.Append( appliedNow ? " → equipped" : "" );
			report.AppendLine();
		}

		var reportText = report.ToString().TrimEnd();
		if ( CompatibilityReport != reportText )
			CompatibilityReport = reportText;
	}

	bool IsToggleOn( AimboxAttachmentId attachment ) => attachment switch
	{
		AimboxAttachmentId.HoloSight => HoloSight,
		AimboxAttachmentId.RaisedRedDot => RaisedRedDot,
		AimboxAttachmentId.RangedSight => RangedSight,
		AimboxAttachmentId.ExtendedMag => ExtendedMag,
		AimboxAttachmentId.ForegripStraight => ForegripStraight,
		AimboxAttachmentId.ForegripAngled => ForegripAngled,
		AimboxAttachmentId.Flashlight => Flashlight,
		AimboxAttachmentId.Suppressor => Suppressor,
		_ => false
	};

	int ComputeRevision()
	{
		var hash = new HashCode();
		hash.Add( Weapon );
		hash.Add( HoloSight );
		hash.Add( RaisedRedDot );
		hash.Add( RangedSight );
		hash.Add( ExtendedMag );
		hash.Add( ForegripStraight );
		hash.Add( ForegripAngled );
		hash.Add( Flashlight );
		hash.Add( Suppressor );
		return hash.ToHashCode();
	}

	static AimboxPlayerController FindLocalPlayer( Scene scene ) =>
		scene?.GetAllComponents<AimboxPlayerController>().FirstOrDefault( p => !p.IsProxy );

	string DescribeToggles() =>
		$"Holo={HoloSight} RMR={RaisedRedDot} Ranged={RangedSight} ExtMag={ExtendedMag} FGripS={ForegripStraight} FGripA={ForegripAngled} Flash={Flashlight} Supp={Suppressor}";
}
