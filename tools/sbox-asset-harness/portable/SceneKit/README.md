# Portable SceneKit (drop-in)

Copy these `.cs` files into a game’s `Code/` tree when that game does **not** already have `MeshPrimitives` / scenery helpers.

1. Copy `SceneKit.*.cs` into e.g. `mygame/Code/SceneKit/`
2. Change `namespace SceneKit` to the game namespace **or** leave as `SceneKit` and reference it
3. Ensure the game project compiles Sandbox APIs (`GameObject`, `ModelRenderer`, etc.)
4. Spawn example:

```csharp
SceneKit.KitProps.Tree( parent, "Tree", new Vector3( 0, 0, 0 ), height: 220 );
```

Prefer adapting an existing game’s helpers over copying these if equivalents already exist.
