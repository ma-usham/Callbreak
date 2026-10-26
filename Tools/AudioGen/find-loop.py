# Finds music loop points for ArtSource/Audio/Sources/sources.json where the beat alone is too weak for
# find-loop.js (soft or drone-heavy tracks): the loop [a, b] whose music leading into b best matches the
# music leading into a, so Recorded.cs's crossfade of x[b-fade..b] into x[a-fade..a] is not heard.
# Coarse search in 50 ms frames: for every loop length and start, the cosine similarity of chroma (12)
# and 24 log bands over the `context` seconds before a and before b ("texture", 1 = the same music).
# Then b is moved by up to 0.15 s to line up the onsets in 10 ms frames ("rhythm").
# Needs Python 3 with numpy and scipy. Usage:
#   python -P Tools/AudioGen/find-loop.py <file.wav> <from s> <to s> <min length s> <max length s> [context s] [top n]
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import stft

path = sys.argv[1]
s0, s1, lmin, lmax = (float(v) for v in sys.argv[2:6])
ctx = float(sys.argv[6]) if len(sys.argv) > 6 else 6.0
top = int(sys.argv[7]) if len(sys.argv) > 7 else 6

fs, x = wavfile.read(path)
m = x.astype(np.float64).mean(1) / 32768 if x.ndim > 1 else x.astype(np.float64) / 32768

hop = int(0.05 * fs)
f, _, Z = stft(m, fs, nperseg=8192, noverlap=8192 - hop, boundary=None, padded=False)
P = np.abs(Z) ** 2
valid = (f > 55) & (f < 5000)
pc = np.round(69 + 12 * np.log2(f[valid] / 440)).astype(int) % 12
chroma = np.log(np.stack([P[valid][pc == k].sum(0) for k in range(12)]) + 1e-10)
edges = np.geomspace(60, 12000, 25)
bands = np.log(np.stack([P[(f >= lo) & (f < hi)].sum(0) for lo, hi in zip(edges[:-1], edges[1:])]) + 1e-10)


def unit(a):
    a = a - a.mean(0, keepdims=True)
    return a / (np.linalg.norm(a, axis=0, keepdims=True) + 1e-9)


F = np.vstack([unit(chroma), unit(bands)]).T / np.sqrt(2)     # frames x 36, unit length
T = len(F)
fr = lambda sec: int(round(sec / 0.05))
C = fr(ctx)
best = []
for L in range(fr(lmin), fr(lmax) + 1):
    sim = np.einsum('ij,ij->i', F[:T - L], F[L:])               # frame t against frame t + L
    cs = np.concatenate([[0], np.cumsum(sim)])
    lo, hi = max(fr(s0) + C, 0), min(fr(s1) - L, T - L)
    if hi <= lo:
        continue
    a = np.arange(lo, hi)
    score = (cs[a] - cs[a - C]) / C
    i = int(np.argmax(score))
    best.append((score[i], a[i], L))
best.sort(reverse=True)

h = fs // 100
n = len(m) // h * h
e = np.log(np.add.reduceat(m[:n] ** 2, np.arange(0, n, h)) + 1e-9)
o = np.maximum(0, np.diff(e, prepend=e[0]))


def onsets(a, b, w):
    u, v = o[a - w:a] - o[a - w:a].mean(), o[b - w:b] - o[b - w:b].mean()
    return float(u @ v / (np.linalg.norm(u) * np.linalg.norm(v) + 1e-12))


print(f"{path.replace(chr(92), '/').split('/')[-1]}: {s0:.0f}-{s1:.0f} s, loops of {lmin:.0f}-{lmax:.0f} s, {ctx:.0f} s compared")
seen = []
for score, a, L in best:
    if any(abs(a - a2) < fr(4) and abs(L - L2) < fr(4) for a2, L2 in seen):
        continue
    seen.append((a, L))
    A, B, w = a * 5, (a + L) * 5, int(ctx * 100)                  # 10 ms frames
    B = max(range(B - 15, B + 16), key=lambda bb: onsets(A, bb, w))
    print(f"  loop {A / 100:7.2f} - {B / 100:7.2f} s = {(B - A) / 100:6.2f} s, texture {score:.3f}, rhythm {onsets(A, B, w):.3f}")
    if len(seen) >= top:
        break
