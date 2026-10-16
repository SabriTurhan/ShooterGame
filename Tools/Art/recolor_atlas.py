"""Recolours an environment atlas for a new world: only the hues in [hue_min, hue_max] move to the target hue.
usage: recolor_atlas.py source.png target.png hue_min hue_max target_hue saturation_mult value_mult value_add"""
import colorsys
import sys

from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
hue_min, hue_max, target_hue, sat_mult, val_mult, val_add = map(float, sys.argv[3:9])

image = Image.open(src).convert("RGBA")
pixels = image.load()
cache = {}
for y in range(image.height):
    for x in range(image.width):
        r, g, b, a = pixels[x, y]
        key = (r, g, b)
        if key not in cache:
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if hue_min <= h <= hue_max and s > 0.06:
                # keep the relative hue spread so shading bands stay distinct
                h = (target_hue + (h - (hue_min + hue_max) / 2) * 0.35) % 1.0
                s = min(1.0, s * sat_mult)
                v = min(1.0, v * val_mult + val_add)
            cache[key] = tuple(int(round(c * 255)) for c in colorsys.hsv_to_rgb(h, s, v))
        pixels[x, y] = cache[key] + (a,)
image.save(dst)
