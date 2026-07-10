namespace Sandbox;

/// <summary>
/// Built-in Facepunch Citizen model — ships with the Citizen addon
/// (https://sbox.game/dev/doc/assets/ready-to-use-assets/citizen-characters).
/// Optional: add <c>facepunch.citizen</c> to <c>thorns.sbproj</c> <c>PackageReferences</c> so remote clients download the same Citizen package;
/// local dev usually resolves from the editor-mounted <c>addons/citizen</c> install without that reference.
/// </summary>
public static class ThornsCitizenPaths
{
	public const string CitizenVmdl = "models/citizen/citizen.vmdl";
}
