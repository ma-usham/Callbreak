// Green-screen key for ChatGPT's tables on #00FF00 (warm subjects, no green in them).
// keyGreen(img) -> { alpha: Float32Array, fg: [r,g,b] Float32Arrays (straight colour, despilled) }
'use strict';
function keyGreen(img) {
  const N = img.w * img.h, d = img.data;
  const alpha = new Float32Array(N), fg = [new Float32Array(N), new Float32Array(N), new Float32Array(N)];
  for (let i = 0; i < N; i++) {
    const r = d[i * 4], g = d[i * 4 + 1], b = d[i * 4 + 2];
    // green excess over the other channels: ~255 on the screen, <= ~20 on the warm table
    const ex = g - Math.max(r, b);
    let a = 1 - (ex - 25) / (230 - 25);
    a = a < 0 ? 0 : a > 1 ? 1 : a;
    alpha[i] = a;
    if (a <= 0) { fg[0][i] = r; fg[1][i] = Math.min(g, Math.max(r, b)); fg[2][i] = b; continue; }
    // unmix the green screen: obs = a*F + (1-a)*(0,255,0)
    let R = r / a, G = (g - (1 - a) * 255) / a, B = b / a;
    // despill: the table never has more green than red
    G = Math.min(G, Math.max(R, B));
    fg[0][i] = Math.min(255, Math.max(0, R)); fg[1][i] = Math.min(255, Math.max(0, G)); fg[2][i] = Math.min(255, Math.max(0, B));
  }
  return { alpha, fg };
}
module.exports = { keyGreen };
