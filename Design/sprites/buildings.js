// Blockout building sprites for Otsukai (16 px per tile, 3/4 top-down view, light from the top left).
// Run: node Design/sprites/buildings.js   (writes previews into Design/sprites/)
const path = require("path");
const P = require(path.join(__dirname, "../../.claude/skills/codixel/core/pixel.js"));

const T = 16; // pixels per tile

// House: w x h tiles. The roof takes the top part, the front wall the rest (door at the bottom centre).
function house(wTiles, hTiles, opt = {}) {
  const W = wTiles * T, H = hTiles * T;
  const b = new P.Buf(W, H);
  const roof = P.hueRamp(5, { hue: opt.roofHue ?? 215, sat: opt.roofSat ?? 0.28, shift: opt.roofShift ?? 60, light: [0.16, 0.52] });
  const wall = P.hueRamp(4, { hue: opt.wallHue ?? 40, sat: 0.3, shift: 8, light: [0.58, 0.86] });
  const wood = P.hueRamp(4, { hue: 25, sat: 0.45, shift: 30, light: [0.16, 0.46] });
  const glass = P.hueRamp(3, { hue: 205, sat: 0.3, shift: 20, light: [0.3, 0.7] });
  const stone = P.hueRamp(2, { hue: 220, sat: 0.08, shift: 10, light: [0.35, 0.48] });

  const roofBottom = Math.round(H * (opt.roofShare ?? 0.4));   // eave line
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

// Supermarket: flat roof seen from above, a coloured sign band, glass front with a double door
function supa(wTiles, hTiles, opt = {}) {
  const W = wTiles * T, H = hTiles * T;
  const b = new P.Buf(W, H);
  const roof = P.hueRamp(4, { hue: 215, sat: 0.06, shift: 10, light: [0.42, 0.74] });
  const wall = P.hueRamp(3, { hue: 40, sat: 0.12, shift: 8, light: [0.66, 0.86] });
  const sign = P.hueRamp(3, { hue: opt.signHue ?? 355, sat: 0.62, shift: 15, light: [0.32, 0.55] });
  const glass = P.hueRamp(3, { hue: 200, sat: 0.32, shift: 20, light: [0.32, 0.72] });
  const frame = P.hueRamp(2, { hue: 215, sat: 0.08, shift: 10, light: [0.3, 0.44] });
  const stone = P.hueRamp(2, { hue: 220, sat: 0.08, shift: 10, light: [0.35, 0.48] });

  const roofBottom = Math.round(H * 0.34), signH = 8;
  // Flat roof: concrete with a lighter parapet on the front edge and a darker line where it meets the far edge
  b.rect(0, 2, W, roofBottom, roof[1]);
  b.rect(0, 2, W, 4, roof[0]);
  b.rect(0, roofBottom - 3, W, roofBottom, roof[3]);
  b.rect(0, roofBottom - 4, W, roofBottom - 3, roof[2]);
  for (let x = 12; x < W - 8; x += 24) b.rect(x, 7, x + 8, 12, roof[2]);   // AC units on the roof
  for (let x = 12; x < W - 8; x += 24) b.rect(x, 12, x + 8, 13, roof[0]);

  // Sign band across the top of the front wall
  b.rect(0, roofBottom, W, roofBottom + signH, sign[1]);
  b.rect(0, roofBottom, W, roofBottom + 1, sign[2]);
  b.rect(0, roofBottom + signH - 1, W, roofBottom + signH, sign[0]);
  b.rect(W / 2 - 10, roofBottom + 2, W / 2 + 10, roofBottom + signH - 2, wall[2]); // logo plate

  // Wall and glass front
  const top = roofBottom + signH;
  b.rect(0, top, W, H, wall[1]);
  b.rect(0, top, 1, H, wall[2]);
  b.rect(W - 1, top, W, H, wall[0]);
  const gy0 = top + 4, gy1 = H - 7;   // wall strip under the glass so the door stands out
  b.rect(4, gy0, W - 4, gy1, glass[1]);
  b.rect(4, gy0, W - 4, gy0 + 2, glass[0]);
  for (let x = 4; x <= W - 4; x += 12) b.rect(x, gy0, x + 1, gy1, frame[1]);        // mullions
  b.rect(4, gy0 - 1, W - 4, gy0, frame[0]); b.rect(4, gy1, W - 4, gy1 + 1, frame[0]);
  // Double automatic door: 2 tiles wide, 1.5 tiles tall, bottom centre
  const dw = 2 * T, dh = Math.round(T * 1.5), dx = Math.round((W - dw) / 2), dy = H - 2 - dh;
  b.rect(dx - 2, dy - 2, dx + dw + 2, H - 2, frame[0]);
  b.rect(dx, dy, dx + dw, H - 2, glass[0]);
  b.rect(dx + dw / 2, dy, dx + dw / 2 + 1, H - 2, frame[0]);
  b.rect(dx, dy, dx + dw, dy + 2, glass[0]);
  for (const gx of [dx + 3, dx + dw / 2 + 3]) { b.pset(gx, dy + 5, glass[2]); b.pset(gx + 1, dy + 4, glass[2]); }
  b.rect(0, H - 2, W, H, stone[0]);
  return b;
}

module.exports = { house, supa };

if (require.main === module) {
  const neighbour = house(5, 5);
  const player = house(5, 5, { roofHue: 8, roofSat: 0.42, roofShift: 18 });   // player's house: warm red roof, easy to spot
  const store = supa(8, 6);
  // preview: all three on grass at 1x, side by side, and each at 3x
  const scene = new P.Buf(16 * 22, 16 * 8);
  scene.clear([120, 170, 90]);
  const put = (s, ox, oy) => { for (let y = 0; y < s.h; y++) for (let x = 0; x < s.w; x++) { const c = s.px[y * s.w + x]; if (c >>> 24) scene.px[(y + oy) * scene.w + x + ox] = c; } };
  put(neighbour, 8, 40); put(player, 104, 40); put(store, 200, 24);
  P.save(P.sheet([{ buf: scene, label: "1x on grass" }, { buf: neighbour, label: "neighbour 3x", scale: 3 },
                  { buf: player, label: "player 3x", scale: 3 }, { buf: store, label: "supa 3x", scale: 3 }], { cols: 2 }),
         path.join(__dirname, "preview_house.png"));
  for (const [n, s] of [["neighbour", neighbour], ["player", player], ["supa", store]])
    console.log(n, s.w + "x" + s.h, "colors:", P.colorCount(s).length, "edge:", JSON.stringify(P.edgeCheck(s)).slice(0, 60));
}
