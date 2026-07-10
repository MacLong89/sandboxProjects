"""
Generate a seamless equirectangular painterly sky for Thorns (2048x1024).
Clouds are sampled from 3D noise on the unit sphere so longitude wraps cleanly.
Run: python tools/generate_thorns_skybox.py
"""
from __future__ import annotations

import math
import os
from array import array

try:
	import numpy as np
except ImportError as e:
	raise SystemExit( "numpy required: pip install numpy" ) from e

try:
	from PIL import Image
except ImportError as e:
	raise SystemExit( "Pillow required: pip install pillow" ) from e

W = 2048
H = 1024
OUT = os.path.join(
	os.path.dirname( __file__ ),
	"..",
	"Assets",
	"materials",
	"skybox",
	"thorns_skybox_painterly_smallclouds_2048.png",
)


def fade(t: np.ndarray) -> np.ndarray:
	return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def lerp(a: np.ndarray, b: np.ndarray, t: np.ndarray) -> np.ndarray:
	return a + t * (b - a)


def make_perm(seed: int = 1337) -> np.ndarray:
	rng = np.random.default_rng( seed )
	p = np.arange( 256, dtype=np.int32 )
	rng.shuffle( p )
	return np.tile( p, 2 )


PERM = make_perm( 42069 )


def grad3(h: np.ndarray, x: np.ndarray, y: np.ndarray, z: np.ndarray) -> np.ndarray:
	h = h & 15
	u = np.where( h < 8, x, y )
	v = np.where( h < 4, y, np.where( (h == 12) | (h == 14), x, z ) )
	return np.where( (h & 1) == 0, u, -u ) + np.where( (h & 2) == 0, v, -v )


def perlin3(x: np.ndarray, y: np.ndarray, z: np.ndarray) -> np.ndarray:
	xi = np.floor( x ).astype( np.int32 ) & 255
	yi = np.floor( y ).astype( np.int32 ) & 255
	zi = np.floor( z ).astype( np.int32 ) & 255
	xf = x - np.floor( x )
	yf = y - np.floor( y )
	zf = z - np.floor( z )
	u, v, w = fade( xf ), fade( yf ), fade( zf )

	def hash3(a, b, c):
		return PERM[PERM[PERM[a] + b] + c]

	aaa = hash3( xi, yi, zi )
	aba = hash3( xi + 1, yi, zi )
	aab = hash3( xi, yi + 1, zi )
	abb = hash3( xi + 1, yi + 1, zi )
	baa = hash3( xi, yi, zi + 1 )
	bba = hash3( xi + 1, yi, zi + 1 )
	bab = hash3( xi, yi + 1, zi + 1 )
	bbb = hash3( xi + 1, yi + 1, zi + 1 )

	x1 = lerp( grad3( aaa, xf, yf, zf ), grad3( aba, xf - 1, yf, zf ), u )
	x2 = lerp( grad3( aab, xf, yf - 1, zf ), grad3( abb, xf - 1, yf - 1, zf ), u )
	y1 = lerp( x1, x2, v )
	x1 = lerp( grad3( baa, xf, yf, zf - 1 ), grad3( bba, xf - 1, yf, zf - 1 ), u )
	x2 = lerp( grad3( bab, xf, yf - 1, zf - 1 ), grad3( bbb, xf - 1, yf - 1, zf - 1 ), u )
	y2 = lerp( x1, x2, v )
	return lerp( y1, y2, w )


def fbm3(x, y, z, octaves=5, lac=2.0, gain=0.5):
	amp = 1.0
	freq = 1.0
	total = np.zeros_like( x, dtype=np.float32 )
	norm = 0.0
	for _ in range( octaves ):
		total += amp * perlin3( x * freq, y * freq, z * freq )
		norm += amp
		amp *= gain
		freq *= lac
	return total / norm


def smoothstep(edge0, edge1, x):
	t = np.clip( (x - edge0) / (edge1 - edge0), 0.0, 1.0 )
	return t * t * (3.0 - 2.0 * t)


def sky_gradient(elev: np.ndarray) -> np.ndarray:
	"""elev in [0,1] — 0 horizon band, 1 zenith."""
	zenith = np.array( [0.18, 0.48, 0.92], dtype=np.float32 )
	mid = np.array( [0.34, 0.62, 0.98], dtype=np.float32 )
	horizon = np.array( [0.72, 0.86, 0.98], dtype=np.float32 )
	t = np.clip( elev, 0.0, 1.0 )
	c = np.empty( (*elev.shape, 3), dtype=np.float32 )
	for i in range( 3 ):
		c[..., i] = np.where(
			t < 0.55,
			lerp( horizon[i], mid[i], t / 0.55 ),
			lerp( mid[i], zenith[i], (t - 0.55) / 0.45 ),
		)
	return c


def main():
	xs = np.linspace( 0, W - 1, W, dtype=np.float32 )
	ys = np.linspace( 0, H - 1, H, dtype=np.float32 )
	xg, yg = np.meshgrid( xs, ys )

	lon = (xg / W) * (2.0 * math.pi)
	lat = (0.5 - yg / H) * math.pi
	clat = np.cos( lat )
	sx = clat * np.cos( lon )
	sy = clat * np.sin( lon )
	sz = np.sin( lat )

	elev = np.clip( (sz + 0.08) / 1.08, 0.0, 1.0 )
	sky = sky_gradient( elev )

	# Large billowy clouds: lower spatial frequency = bigger features on the sphere.
	scale_a = 2.0
	scale_b = 3.2
	scale_c = 5.0
	wisp = 1.1

	n1 = fbm3( sx * scale_a + 1.7, sy * scale_a + 0.4, sz * scale_a + 2.1, octaves=5 )
	n2 = fbm3( sx * scale_b + 4.2, sy * scale_b + 1.9, sz * scale_b + 0.6, octaves=4 )
	n3 = fbm3( sx * scale_c + 2.8, sy * scale_c + 3.3, sz * scale_c + 5.0, octaves=3 )
	w1 = fbm3( sx * wisp + 8.0, sy * wisp + 1.2, sz * wisp + 6.4, octaves=3 )

	density = (
		0.62 * smoothstep( 0.18, 0.48, n1 )
		+ 0.48 * smoothstep( 0.20, 0.50, n2 )
		+ 0.36 * smoothstep( 0.22, 0.52, n3 )
		+ 0.16 * smoothstep( 0.26, 0.56, w1 )
	)
	density *= smoothstep( -0.05, 0.22, sz )
	density *= smoothstep( 0.08, 0.35, elev )
	density = np.clip( density, 0.0, 1.0 )

	# Painterly bands: cloud self-shading only (no fixed zenith sun — runtime sun disc handles that).
	light = np.clip( 0.55 + 0.12 * n1, 0.0, 1.0 )
	band = np.floor( light * 4.0 ) / 4.0
	cloud_lit = np.array( [0.98, 0.97, 0.90], dtype=np.float32 )
	cloud_shadow = np.array( [0.78, 0.86, 0.96], dtype=np.float32 )
	cloud = cloud_shadow + (cloud_lit - cloud_shadow) * band[..., None]

	alpha = smoothstep( 0.10, 0.58, density )
	out = sky * (1.0 - alpha[..., None]) + cloud * alpha[..., None]
	out = np.clip( out, 0.0, 1.0 )

	# Seam check (longitude wrap).
	seam_delta = np.abs( out[:, 0, :] - out[:, -1, :] ).mean()
	print( f"Seam mean delta (col 0 vs col {W-1}): {seam_delta:.6f}" )

	pixels = (out * 255.0 + 0.5).astype( np.uint8 )
	img = Image.fromarray( pixels, mode="RGB" )
	out_path = os.path.normpath( OUT )
	os.makedirs( os.path.dirname( out_path ), exist_ok=True )
	img.save( out_path, optimize=True )
	print( f"Wrote {out_path} ({W}x{H})" )


if __name__ == "__main__":
	main()
