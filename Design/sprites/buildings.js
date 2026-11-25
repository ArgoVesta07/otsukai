// Blockout building sprites for Otsukai (16 px per tile, 3/4 top-down view, light from the top left).
// Run: node Design/sprites/buildings.js   (writes previews into Design/sprites/)
const path = require("path");
const P = require(path.join(__dirname, "../../.claude/skills/codixel/core/pixel.js"));

const T = 16; // pixels per tile

// House: w x h tiles. The roof takes the top part, the front wall the rest (door at the bottom centre).
function house(wTiles, hTiles, opt = {}) {
  const W = wTiles * T, H = hTiles * T;
  const b = new P.Buf(W, H);
  const roof = P.hueRamp(5, { hue: opt.roofHue ?? 215, sat: opt.roofSat ?? 0.28, light: [0.16, 0.52] });
  const wall = P.hueRamp(4, { hue: opt.wallHue ?? 40, sat: 0.3, shift: 8, light: [0.58, 0.86] });
  const wood = P.hueRamp(4, { hue: 25, sat: 0.45, shift: 30, light: [0.16, 0.46] });
  const glass = P.hueRamp(3, { hue: 205, sat: 0.3, shift: 20, light: [0.3, 0.7] });
  const stone = P.hueRamp(2, { hue: 220, sat: 0.08, shift: 10, light: [0.35, 0.48] });

  const roofBottom = Math.round(H * 0.55);   // eave line
  const inset = 3;                           // roof overhangs the wall by this much on each side

  // Roof: rows of tiles every 4 px, each row one flat tone, lighter near the ridge (faces the sky)
  const rows = Math.ceil((roofBottom - 2) / 4);
  for (let y = 2; y < roofBottom; y++) {
    const row = Math.floor((y - 2) / 4);
    const base = 3 - Math.floor(row * 3 / rows);                   // 3 at the ridge .. 1 at the eave
    for (let x = 0; x < W; x++) {
      let k = base;
      if ((y - 2) % 4 === 3) k = base - 1;                          // row seam: one step darker
      else if ((x + (row % 2) * 3) % 6 === 0) k = base - 1;         // tile joints, offset per row
      b.pset(x, y, roof[Math.max(0, k)]);
    }
  }
  b.rect(0, 2, W, 4, roof[4]);                 // ridge cap, catches the light
  b.rect(0, roofBottom - 2, W, roofBottom, roof[0]); // eave edge

  // Front wall with the eave shadow along its top
  b.rect(inset, roofBottom, W - inset, H, wall[2]);
  b.rect(inset, roofBottom, inset + 1, H, wall[3]);          // left edge faces the light
  b.rect(W - inset - 1, roofBottom, W - inset, H, wall[1]);  // right edge in shade
  b.rect(inset, roofBottom, W - inset, roofBottom + 2, wall[0]);
  b.rect(inset, H - 2, W - inset, H, stone[0]);              // foundation
  b.rect(inset, H - 3, W - inset, H - 2, stone[1]);

  // Door: 1 x 1.5 tiles at the bottom centre
  const dw = T, dh = Math.round(T * 1.5), dx = Math.round((W - dw) / 2), dy = H - 2 - dh;
  b.rect(dx - 1, dy - 1, dx + dw + 1, H - 2, wood[0]);       // frame
  b.rect(dx, dy, dx + dw, H - 2, wood[2]);
  b.rect(dx + Math.floor(dw / 2), dy, dx + Math.floor(dw / 2) + 1, H - 2, wood[1]); // middle seam (sliding door)
  b.rect(dx, dy, dx + dw, dy + 1, wood[3]);
  b.pset(dx + dw / 2 - 3, dy + dh / 2, wood[3]);
  b.pset(dx + dw / 2 + 2, dy + dh / 2, wood[3]);

  // Two windows, one on each side of the door
  const ww = 12, wh = 10, wy = roofBottom + Math.round((H - 2 - roofBottom - wh) / 2) - 2;
  for (const wx of [Math.round((dx - inset - ww) / 2) + inset, W - Math.round((dx - inset - ww) / 2) - inset - ww]) {
    b.rect(wx - 1, wy - 1, wx + ww + 1, wy + wh + 1, wood[1]);
    b.rect(wx, wy, wx + ww, wy + wh, glass[1]);
    b.rect(wx, wy, wx + ww, wy + 2, glass[0]);                 // eave shadow on the glass
    b.rect(wx + ww / 2, wy, wx + ww / 2 + 1, wy + wh, wood[1]); // mullion
    b.pset(wx + 2, wy + 4, glass[2]); b.pset(wx + 3, wy + 3, glass[2]); // glint, top left
  }
  return b;
}

module.exports = { house };

if (require.main === module) {
  const h = house(5, 5);
  // preview: the sprite on grass at 1x and 4x
  const scene = new P.Buf(112, 112);
  scene.clear([120, 170, 90]);
  scene.sprite ? scene.sprite(h, 16, 16) : null;
  for (let y = 0; y < h.h; y++) for (let x = 0; x < h.w; x++) { const c = h.px[y * h.w + x]; if (c >>> 24) scene.px[(y + 16) * scene.w + x + 16] = c; }
  P.save(P.sheet([{ buf: scene, label: "1x on grass" }, { buf: h, label: "4x", scale: 4 }]), path.join(__dirname, "preview_house.png"));
  console.log("colors:", P.colorCount(h).length, "edge:", JSON.stringify(P.edgeCheck(h)).slice(0, 80));
}
