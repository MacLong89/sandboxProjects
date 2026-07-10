namespace Fauna2;

/// <summary>
/// Lightweight in-process event hub used to decouple systems.
/// Events fire on whichever machine raised them (most are host-side).
/// Network-visible notifications go through ZooState.Notify instead.
/// </summary>
public static class GameEvents
{
	public static event Action<AnimalComponent> AnimalSpawned;
	public static event Action<AnimalComponent> AnimalRemoved;
	public static event Action<AnimalComponent> AnimalBred;
	public static event Action<string> SpeciesDiscovered;
	public static event Action<string> VariantDiscovered;
	public static event Action<int> LevelUp;
	public static event Action PlotPurchased;
	public static event Action HabitatPlaced;
	public static event Action ZooModified;
	public static event Action ZooReset;
	public static event Action<string, string> DevPreviewChanged;
	public static event Action<int> EconomyGain;

	public static void RaiseAnimalSpawned( AnimalComponent a ) => AnimalSpawned?.Invoke( a );
	public static void RaiseAnimalRemoved( AnimalComponent a ) => AnimalRemoved?.Invoke( a );
	public static void RaiseAnimalBred( AnimalComponent a ) => AnimalBred?.Invoke( a );
	public static void RaiseSpeciesDiscovered( string speciesId ) => SpeciesDiscovered?.Invoke( speciesId );
	public static void RaiseVariantDiscovered( string key ) => VariantDiscovered?.Invoke( key );
	public static void RaiseLevelUp( int newLevel ) => LevelUp?.Invoke( newLevel );
	public static void RaisePlotPurchased() => PlotPurchased?.Invoke();
	public static void RaiseHabitatPlaced() => HabitatPlaced?.Invoke();
	public static void RaiseZooModified() => ZooModified?.Invoke();
	public static void RaiseZooReset() => ZooReset?.Invoke();
	public static void RaiseDevPreviewChanged( string definitionId, string variantId ) =>
		DevPreviewChanged?.Invoke( definitionId, variantId );
	public static void RaiseEconomyGain( int amount ) => EconomyGain?.Invoke( amount );
}
