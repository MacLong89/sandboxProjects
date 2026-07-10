"""
Generate a soft radial sun-disc texture for the Thorns sky billboard.
Run: python tools/generate_thorns_sun_disc.py
"""
from __future__ import annotations

import math
import os

try:
	import numpy as np
except ImportError as e:
	raise SystemExit( "numpy required: pip install numpy" ) from e

try:
	from PIL import Image
except ImportError as e:
	raise SystemExit( "Pillow required: pip install pillow" ) from e

SIZE = 512
OUT = os.path.join(
	os.path.dirname( __file__ ),
	"..",
	"Assets",
	"materials",
	"skybox",
	"thorns_sun_disc.png",
)


def main():
	xy = np.linspace( -1.0, 1.0, SIZE, dtype=np.float32 )
	xg, yg = np.meshgrid( xy, xy )
	r = np.sqrt( xg * xg + yg * yg )

	core = np.clip( 1.0 - r / 0.22, 0.0, 1.0 )
	glow = np.clip( 1.0 - (r - 0.08) / 0.55, 0.0, 1.0 )
	alpha = np.clip( np.power( core, 0.35 ) + np.power( glow, 3.0 ) * 0.28, 0.0, 1.0 )

	rgb = np.empty( (SIZE, SIZE, 3), dtype=np.float32 )
	rgb[..., 0] = 1.0
	rgb[..., 1] = 0.58 + 0.14 * (1.0 - r)
	rgb[..., 2] = 0.08 + 0.10 * (1.0 - r)
	rgb *= alpha[..., None]

	pixels = np.concatenate( [rgb, alpha[..., None]], axis=-1 )
	pixels = (np.clip( pixels, 0.0, 1.0 ) * 255.0 + 0.5).astype( np.uint8 )

	out_path = os.path.normpath( OUT )
	os.makedirs( os.path.dirname( out_path ), exist_ok=True )
	Image.fromarray( pixels, mode="RGBA" ).save( out_path, optimize=True )
	print( f"Wrote {out_path} ({SIZE}x{SIZE})" )


if __name__ == "__main__":
	main()
