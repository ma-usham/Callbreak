"""Renders an SVG to a PNG at an exact pixel size with headless Microsoft Edge (no other SVG renderer
is needed). An opaque picture renders once; a transparent one renders on black and on white and its
alpha is recovered from the difference.

Usage (as a module): render(svg_path, png_path, width, height, transparent=False)
"""
import os
import subprocess
import tempfile

import numpy as np
from PIL import Image

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"


def _shot(svg_path, width, height, background):
    svg_url = "file:///" + os.path.abspath(svg_path).replace("\\", "/")
    with tempfile.TemporaryDirectory() as tmp:
        page = os.path.join(tmp, "page.html")
        shot = os.path.join(tmp, "shot.png")
        with open(page, "w", encoding="utf-8") as fh:
            fh.write(f'<!doctype html><html><body style="margin:0;background:{background}">'
                     f'<img src="{svg_url}" style="display:block;width:{width}px;height:{height}px"></body></html>')
        args = [EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                "--allow-file-access-from-files", "--virtual-time-budget=10000", f"--user-data-dir={os.path.join(tmp, 'profile')}",
                f"--window-size={width},{height}", f"--screenshot={shot}", "file:///" + page.replace("\\", "/")]
        # Headless Edge now and then never exits after its screenshot: give it a while, then try again.
        for attempt in range(3):
            try:
                subprocess.run(args, check=True, capture_output=True, timeout=90)
                break
            except subprocess.TimeoutExpired:
                if os.path.exists(shot) and os.path.getsize(shot) > 0:
                    break
                if attempt == 2:
                    raise
        image = Image.open(shot).convert("RGB")
        image.load()
    if image.size != (width, height):
        raise RuntimeError(f"{svg_path}: Edge drew {image.size}, not {width} x {height}")
    return np.asarray(image, dtype=np.float32) / 255.0


def render(svg_path, png_path, width, height, transparent=False):
    if not transparent:
        rgb = _shot(svg_path, width, height, "#000")
        Image.fromarray((rgb * 255 + 0.5).astype(np.uint8), "RGB").save(png_path, optimize=True)
        return
    on_black = _shot(svg_path, width, height, "#000")
    on_white = _shot(svg_path, width, height, "#fff")
    alpha = np.clip(1.0 - (on_white - on_black).mean(axis=2), 0.0, 1.0)
    colour = np.where(alpha[..., None] > 1e-3, on_black / np.maximum(alpha[..., None], 1e-3), 0.0)
    rgba = np.dstack([np.clip(colour, 0, 1), alpha])
    Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA").save(png_path, optimize=True)
