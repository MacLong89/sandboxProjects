using System.Drawing;
using System.Drawing.Imaging;

// Generates simple solid + icon placeholder PNGs for NO FLY UI/materials.
var outDir = @"C:\Users\Macra\Projects\sandboxProjects\no_fly\Assets\ui\icons";
Directory.CreateDirectory(outDir);
Directory.CreateDirectory(@"C:\Users\Macra\Projects\sandboxProjects\no_fly\Assets\materials\nofly");

void SaveSolid(string name, int r, int g, int b, int size = 64)
{
    using var bmp = new Bitmap(size, size);
    using var gfx = Graphics.FromImage(bmp);
    gfx.Clear(Color.FromArgb(255, r, g, b));
    using var pen = new Pen(Color.FromArgb(180, 255, 255, 255), 3);
    gfx.DrawRectangle(pen, 4, 4, size - 9, size - 9);
    bmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
}

var icons = new (string name, int r, int g, int b)[]
{
    ("shirt", 90, 160, 255),
    ("shoe", 120, 70, 40),
    ("book", 220, 100, 70),
    ("laptop", 110, 120, 130),
    ("knife", 190, 190, 200),
    ("usb", 60, 210, 120),
    ("jewel", 80, 220, 255),
    ("package", 180, 130, 70),
    ("passport", 40, 90, 180),
    ("seal", 240, 190, 40),
    ("nofly_logo", 255, 140, 40),
    ("tsa_badge", 255, 200, 40),
};

foreach (var i in icons) SaveSolid(i.name, i.r, i.g, i.b);

// Simple vmat companions (tint-friendly unlit-ish using default shader path)
foreach (var i in icons)
{
    var vmat = $@"<!-- kv3 encoding:text:version{{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d}} format:generic:version{{7412167c-06e9-4698-aff2-e63eb59037e7}} -->
{{
	shader = ""shaders/complex.shader""
	TextureColor = ""ui/icons/{i.name}.png""
	F_DIRECT_LIGHT_ONLY = 1
	g_vColorTint = [{i.r/255f:0.###}, {i.g/255f:0.###}, {i.b/255f:0.###}, 1.0]
}}
";
    File.WriteAllText($@"C:\Users\Macra\Projects\sandboxProjects\no_fly\Assets\materials\nofly\{i.name}.vmat", vmat);
}

Console.WriteLine($"Generated {icons.Length} icons + vmats");
