using System.Collections.Generic;

namespace Sandbox;

/// <summary>Player-facing control reference for menus and hot tips (matches shipped <c>Input.config</c> + Thorns-specific binds).</summary>
public static class ThornsControlsCatalog
{
	public readonly record struct Entry( string Action, string Binding );

	public readonly record struct Section( string Title, IReadOnlyList<Entry> Entries );

	public static readonly Section[] Sections =
	[
		new( "Movement",
		[
			new( "Move", "W A S D" ),
			new( "Sprint", "Shift" ),
			new( "Crouch", "Ctrl" ),
			new( "Jump", "Space" ),
		] ),
		new( "Combat",
		[
			new( "Primary attack / harvest swing", "LMB (Mouse1)" ),
			new( "Aim down sights (guns)", "RMB (Mouse2)" ),
			new( "Reload", "R" ),
		] ),
		new( "Inventory & menu",
		[
			new( "Survival menu (inventory, craft, journal)", "Tab" ),
			new( "World map (fullscreen)", "M" ),
			new( "Close menu / dismiss UI", "Esc" ),
			new( "Hotbar slots 1–8", "1 – 8" ),
			new( "Cycle hotbar", "Mouse wheel" ),
			new( "Use equipped hotbar item (food, bandage, etc.)", "E" ),
			new( "Drag & drop items (in menu)", "LMB + drag" ),
			new( "Quick-move stack (in menu)", "Shift + LMB" ),
		] ),
		new( "World interaction",
		[
			new( "Interact, loot crates, open stations", "E (hold where noted)" ),
			new( "Your doors (doorways you built)", "E to open or close" ),
			new( "Punch trees / rocks (empty hands)", "LMB" ),
			new( "Harvest with equipped tool", "LMB" ),
		] ),
		new( "Building",
		[
			new( "Toggle build mode", "B" ),
			new( "Build toolbar (while in build mode)", "1 – 7" ),
			new( "Rotate placement preview", "R" ),
			new( "Place structure / confirm", "Left click" ),
			new( "Remove / upgrade (select tool, then left click on piece)", "Build toolbar + left click" ),
			new( "Place campfire / chest / workbench / bed", "Craft from [Tab], equip on hotbar, then left click" ),
		] ),
		new( "Taming & mounts",
		[
			new( "Tame weakened wildlife", "Hold E" ),
			new( "Mount your tame", "E (on mountable tame)" ),
			new( "Dismount", "Crouch or E on mount" ),
			new( "Hop while mounted", "Space" ),
		] ),
		new( "Social",
		[
			new( "Server chat", "Enter" ),
		] ),
		new( "Camera",
		[
			new( "Look around", "Mouse" ),
		] ),
	];
}
