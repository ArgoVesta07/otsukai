// Blockout building sprites for Otsukai (16 px per tile, 3/4 top-down view, light from the top left).
// Every sprite has its door at the bottom centre, like the doors in Design/denah-v2.
// Run: node Design/sprites/buildings.js          -> preview_buildings.png (review sheet)
//      node Design/sprites/buildings.js export   -> one PNG per building in Assets/Art/Buildings/
const fs = require("fs");
const path = require("path");
const P = require(path.join(__dirname, "../../.claude/skills/codixel/core/pixel.js"));

const T = 16; // pixels per tile

const ramp = (n, hue, sat, light, shift = 20) => P.hueRamp(n, { hue, sat, shift, light });
const STONE = ramp(2, 220, 0.08, [0.35, 0.48], 10);
const GLASS = ramp(3, 200, 0.32, [0.32, 0.72]);
const FRAME = ramp(2, 215, 0.08, [0.3, 0.44], 10);
const WOOD = ramp(4, 25, 0.45, [0.16, 0.46], 30);

function foundation(b, x0, x1, H) {
  b.rect(x0, H - 2, x1, H, STONE[0]);
  b.rect(x0, H - 3, x1, H - 2, STONE[1]);
}

// Door: tiles wide, 1.5 tiles tall, bottom centre. kind: "wood" (sliding) or "glass" (automatic)
function door(b, W, H, tiles = 1, kind = "wood") {
  const dw = tiles * T, dh = Math.round(T * 1.5), dx = Math.round((W - dw) / 2), dy = H - 2 - dh;
  if (kind === "glass") {
    b.rect(dx - 2, dy - 2, dx + dw + 2, H - 2, FRAME[0]);
    b.rect(dx, dy, dx + dw, H - 2, GLASS[0]);
    b.rect(dx + dw / 2, dy, dx + dw / 2 + 1, H - 2, FRAME[0]);
    for (const gx of [dx + 3, dx + dw / 2 + 3]) { b.pset(gx, dy + 5, GLASS[2]); b.pset(gx + 1, dy + 4, GLASS[2]); }
  } else {
    b.rect(dx - 1, dy - 1, dx + dw + 1, H - 2, WOOD[0]);
    b.rect(dx, dy, dx + dw, H - 2, WOOD[2]);
    b.rect(dx + Math.floor(dw / 2), dy, dx + Math.floor(dw / 2) + 1, H - 2, WOOD[1]);
    b.rect(dx, dy, dx + dw, dy + 1, WOOD[3]);
    b.pset(dx + dw / 2 - 3, dy + dh / 2, WOOD[3]);
    b.pset(dx + dw / 2 + 2, dy + dh / 2, WOOD[3]);
  }
  return { dx, dy, dw, dh };
}

function window(b, x, y, w, h) {
  b.rect(x - 1, y - 1, x + w + 1, y + h + 1, WOOD[1]);
  b.rect(x, y, x + w, y + h, GLASS[1]);
  b.rect(x, y, x + w, y + 2, GLASS[0]);
  b.rect(x + Math.floor(w / 2), y, x + Math.floor(w / 2) + 1, y + h, WOOD[1]);
  b.pset(x + 2, y + 4, GLASS[2]); b.pset(x + 3, y + 3, GLASS[2]);
}

// Tiled roof: rows every 4 px, each row one flat tone, lighter near the ridge
function tiledRoof(b, W, top, bottom, roof) {
  const rows = Math.ceil((bottom - top) / 4);
  for (let y = top; y < bottom; y++) {
    const row = Math.floor((y - top) / 4);
    const base = 3 - Math.floor(row * 3 / rows);
    for (let x = 0; x < W; x++) {
      let k = base;
      if ((y - top) % 4 === 3) k = base - 1;
      else if ((x + (row % 2) * 3) % 6 === 0) k = base - 1;
      b.pset(x, y, roof[Math.max(0, k)]);
    }
  }
  b.rect(0, top, W, top + 2, roof[4]);
  b.rect(0, bottom - 2, W, bottom, roof[0]);
}

// Flat concrete roof seen from above, with a parapet along the front edge
function flatRoof(b, W, top, bottom, units = true) {
  const roof = ramp(4, 215, 0.06, [0.42, 0.74], 10);
  b.rect(0, top, W, bottom, roof[1]);
  b.rect(0, top, W, top + 2, roof[0]);
  b.rect(0, bottom - 3, W, bottom, roof[3]);
  b.rect(0, bottom - 4, W, bottom - 3, roof[2]);
  if (units) for (let x = 12; x < W - 8; x += 24) { b.rect(x, top + 5, x + 8, top + 10, roof[2]); b.rect(x, top + 10, x + 8, top + 11, roof[0]); }
}

// --- Templates -------------------------------------------------------------------------------------

// Japanese house: tiled gable roof, plaster or wooden front wall, sliding door, windows.
// opt.awning: shop awning hue (shotengai); opt.lamp: red lamp over the door (koban)
function house(wT, hT, opt = {}) {
  const W = wT * T, H = hT * T, b = new P.Buf(W, H);
  const roof = ramp(5, opt.roofHue ?? 215, opt.roofSat ?? 0.28, [0.16, 0.52], opt.roofShift ?? 60);
  const wall = opt.wood ? ramp(4, 28, 0.38, [0.3, 0.52], 12) : ramp(4, opt.wallHue ?? 40, opt.wallSat ?? 0.3, [0.58, 0.86], 8);
  const eave = Math.round(H * (opt.roofShare ?? 0.4)), inset = 3;
  b.wall = H - eave;
  tiledRoof(b, W, 2, eave, roof);

  b.rect(inset, eave, W - inset, H, wall[2]);
  if (opt.wood) for (let x = inset + 3; x < W - inset; x += 4) b.rect(x, eave, x + 1, H, wall[1]);  // planks
  b.rect(inset, eave, inset + 1, H, wall[3]);
  b.rect(W - inset - 1, eave, W - inset, H, wall[1]);
  b.rect(inset, eave, W - inset, eave + 2, wall[0]);
  foundation(b, inset, W - inset, H);

  let top = eave + 2;
  if (opt.awning !== undefined) {
    // Striped awning over the shop front
    const aw = ramp(3, opt.awning, opt.awningSat ?? 0.55, [0.36, 0.78], 15);
    for (let x = inset - 1; x < W - inset + 1; x++) {
      const k = Math.floor((x - inset) / 4) % 2 === 0 ? 1 : 2;
      b.rect(x, eave, x + 1, eave + 6, aw[k]);
    }
    b.rect(inset - 1, eave + 6, W - inset + 1, eave + 7, aw[0]);
    top = eave + 7;
  }
  const d = door(b, W, H, opt.doorTiles ?? 1, opt.glassDoor ? "glass" : "wood");
  if (opt.lamp) { b.rect(W / 2 - 2, d.dy - 6, W / 2 + 2, d.dy - 2, [200, 50, 45]); b.pset(W / 2 - 1, d.dy - 5, [240, 120, 110]); }

  if (opt.awning !== undefined) {
    // Shop window on each side of the door, as tall as the door
    for (const [x0, x1] of [[inset + 2, d.dx - 3], [d.dx + d.dw + 3, W - inset - 2]]) {
      if (x1 - x0 < 6) continue;
      b.rect(x0 - 1, d.dy - 1, x1 + 1, H - 4, FRAME[0]);
      b.rect(x0, d.dy, x1, H - 5, GLASS[1]);
      b.rect(x0, d.dy, x1, d.dy + 2, GLASS[0]);
      b.pset(x0 + 2, d.dy + 4, GLASS[2]); b.pset(x0 + 3, d.dy + 3, GLASS[2]);
    }
  } else if (!opt.noWindows) {
    const ww = 12, wh = 10, wy = top + Math.max(2, Math.round((H - 2 - top - wh) / 2) - 2);
    const side = Math.round((d.dx - inset - ww) / 2) + inset;
    if (d.dx - inset > ww + 4) { window(b, side, wy, ww, wh); window(b, W - side - ww, wy, ww, wh); }
  }
  return b;
}

// Modern store: flat roof, coloured sign band, glass front (or windows) with an automatic door
function store(wT, hT, opt = {}) {
  const W = wT * T, H = hT * T, b = new P.Buf(W, H);
  const wall = ramp(3, 40, 0.12, [0.66, 0.86], 8);
  const sign = ramp(3, opt.signHue ?? 355, opt.signSat ?? 0.62, opt.signLight ?? [0.32, 0.55], 15);
  const eave = Math.round(H * (opt.roofShare ?? 0.34)), signH = opt.signH ?? 8;
  b.wall = H - eave;
  flatRoof(b, W, 2, eave);

  b.rect(0, eave, W, eave + signH, sign[1]);
  b.rect(0, eave, W, eave + 1, sign[2]);
  b.rect(0, eave + signH - 1, W, eave + signH, sign[0]);
  if (opt.stripe !== undefined) {                           // second colour stripe (konbini)
    const st = ramp(2, opt.stripe, 0.6, [0.35, 0.55], 10);
    b.rect(0, eave + signH, W, eave + signH + 2, st[1]);
  }
  b.rect(W / 2 - Math.min(10, W / 4), eave + 2, W / 2 + Math.min(10, W / 4), eave + signH - 2, wall[2]); // logo plate

  const top = eave + signH + (opt.stripe !== undefined ? 2 : 0);
  b.rect(0, top, W, H, wall[1]);
  b.rect(0, top, 1, H, wall[2]);
  b.rect(W - 1, top, W, H, wall[0]);
  const gy0 = top + 4, gy1 = H - 7;
  b.rect(3, gy0, W - 3, gy1, GLASS[1]);
  b.rect(3, gy0, W - 3, gy0 + 2, GLASS[0]);
  for (let x = 3; x <= W - 3; x += 12) b.rect(x, gy0, x + 1, gy1, FRAME[1]);
  b.rect(3, gy0 - 1, W - 3, gy0, FRAME[0]); b.rect(3, gy1, W - 3, gy1 + 1, FRAME[0]);
  door(b, W, H, opt.doorTiles ?? (wT >= 7 ? 2 : 1), "glass");
  b.rect(0, H - 2, W, H, STONE[0]);
  return b;
}

// Public building: flat roof, concrete facade with rows of windows, entrance at the bottom centre
function civic(wT, hT, opt = {}) {
  const W = wT * T, H = hT * T, b = new P.Buf(W, H);
  const wall = ramp(3, opt.wallHue ?? 40, opt.wallSat ?? 0.08, opt.wallLight ?? [0.62, 0.84], 10);
  const eave = Math.round(H * (opt.roofShare ?? 0.28));
  b.wall = H - eave;
  flatRoof(b, W, 2, eave);

  b.rect(0, eave, W, H, wall[1]);
  b.rect(0, eave, 1, H, wall[2]);
  b.rect(W - 1, eave, W, H, wall[0]);
  b.rect(0, eave, W, eave + 2, wall[0]);
  let top = eave + 2;
  if (opt.signHue !== undefined) {
    const sign = ramp(3, opt.signHue, 0.55, [0.3, 0.55], 15);
    b.rect(W / 2 - 24, eave + 3, W / 2 + 24, eave + 9, sign[1]);
    b.rect(W / 2 - 24, eave + 3, W / 2 + 24, eave + 4, sign[2]);
    top = eave + 10;
  }
  if (opt.clock) { b.rect(W / 2 - 4, eave + 3, W / 2 + 4, eave + 11, wall[2]); b.ring(W / 2, eave + 7, 3, 1, FRAME[0]); b.pset(W / 2, eave + 6, FRAME[0]); top = eave + 12; }

  const d = door(b, W, H, opt.doorTiles ?? 2, "glass");
  // Window grid: 8x6 windows every 12 px, rows down to the door top, skipping the door column
  for (let y = top + 3; y + 6 <= H - 6; y += 11) {
    for (let x = 6; x + 8 <= W - 6; x += 12) {
      if (y + 6 > d.dy - 3 && x + 8 > d.dx - 4 && x < d.dx + d.dw + 4) continue;
      b.rect(x - 1, y - 1, x + 9, y + 7, FRAME[1]);
      b.rect(x, y, x + 8, y + 6, GLASS[1]);
      b.rect(x, y, x + 8, y + 1, GLASS[0]);
    }
  }
  foundation(b, 0, W, H);
  return b;
}

// Gomi station: concrete pad with a green net cage
function gomi(wT, hT) {
  const W = wT * T, H = hT * T, b = new P.Buf(W, H);
  const net = ramp(3, 130, 0.45, [0.22, 0.48], 15);
  b.rect(0, H - 6, W, H, STONE[1]);
  b.rect(0, H - 2, W, H, STONE[0]);
  b.rect(2, 6, W - 2, H - 6, net[1]);
  for (let x = 4; x < W - 2; x += 4) b.rect(x, 6, x + 1, H - 6, net[0]);
  for (let y = 9; y < H - 6; y += 4) b.rect(2, y, W - 2, y + 1, net[0]);
  b.rect(2, 4, W - 2, 6, net[2]);
  b.wall = H - 4;
  return b;
}

// Japanese post box: red, rounded top, slot, white plate. 1 x 1.5 tiles
function mailbox() {
  const W = T, H = Math.round(T * 1.5), b = new P.Buf(W, H);
  const red = ramp(4, 358, 0.7, [0.25, 0.6], 15);
  b.rect(3, 4, 13, H - 3, red[2]);
  b.rect(4, 3, 12, 4, red[2]); b.rect(5, 2, 11, 3, red[3]);   // rounded top
  b.rect(3, 4, 4, H - 3, red[3]);                               // lit left side
  b.rect(12, 4, 13, H - 3, red[1]);                             // shaded right side
  b.rect(5, 7, 11, 8, red[0]);                                  // letter slot
  b.rect(6, 11, 10, 15, [235, 230, 220]);                       // white plate
  b.rect(6, 15, 10, 16, red[1]);
  b.rect(4, H - 3, 12, H - 1, STONE[0]);                        // base
  b.wall = 6;
  return b;
}

// Bus stop sign: pole with a round blue sign and a small timetable box. 1 x 2 tiles
function busStop() {
  const W = T, H = 2 * T, b = new P.Buf(W, H);
  const blue = ramp(3, 210, 0.6, [0.3, 0.6], 15);
  b.rect(7, 8, 9, H - 2, FRAME[1]);                             // pole
  b.blob(8, 6, 5.5, blue[1]); b.blob(7.5, 5.5, 3.5, blue[2]);   // sign
  b.rect(5, 5, 11, 7, [235, 235, 230]);                         // white band on the sign
  b.rect(5, 16, 11, 22, [220, 220, 210]); b.rect(5, 16, 11, 17, FRAME[0]); // timetable
  b.rect(4, H - 2, 12, H, STONE[0]);                            // base
  b.wall = 3;
  return b;
}

// Signpost (mode board): red-brown pole with two dark arrow boards and yellow tips. 2 x 2 tiles
function signpost() {
  const W = 2 * T, H = 2 * T, b = new P.Buf(W, H);
  const pole = ramp(3, 8, 0.5, [0.2, 0.42], 15);
  const board = ramp(3, 30, 0.18, [0.12, 0.3], 15);
  const yellow = ramp(2, 48, 0.85, [0.45, 0.6], 8);
  const ink = [225, 222, 210];
  b.rect(14, 2, 18, H - 2, pole[1]);
  b.rect(14, 2, 15, H - 2, pole[2]);                             // lit left side
  b.rect(17, 2, 18, H - 2, pole[0]);
  b.rect(14, 1, 18, 2, board[1]);                                // cap
  // Board pointing right (upper) and board pointing left (lower)
  const arrow = (x0, x1, y0, y1, right) => {
    const tip = 4;
    for (let y = y0; y < y1; y++) {
      const half = (y1 - y0) / 2, d = Math.abs(y + 0.5 - (y0 + half));
      const reach = Math.round(tip * (1 - d / half));
      const a = right ? x0 : x0 - reach, c = right ? x1 + reach : x1;
      b.rect(a, y, c, y + 1, board[1]);
    }
    b.rect(x0, y0, x1, y0 + 1, board[2]);                        // top edge catches the light
    b.rect(x0, y1 - 1, x1, y1, board[0]);
    // Yellow triangle near the pointing end, pointing the same way
    const mid = (y0 + y1) / 2, tx = right ? x1 - 4 : x0 + 1;
    for (let i = 0; i < 3; i++) {
      const x = right ? tx + i : tx + 2 - i, h = 3 - i;
      b.rect(x, Math.round(mid - h + 0.5) - 0, x + 1, Math.round(mid + h - 0.5), yellow[1]);
    }
    for (let i = 0; i < 2; i++) b.rect(right ? x0 + 3 + i * 7 : x0 + 4 + i * 7, y0 + 3, (right ? x0 + 3 + i * 7 : x0 + 4 + i * 7) + 5, y0 + 4, ink); // text lines
    b.pset(right ? x0 + 2 : x1 - 3, y0 + 2, [170, 170, 165]);    // bolt
  };
  arrow(4, 26, 4, 12, true);
  arrow(6, 27, 14, 21, false);
  b.rect(12, H - 2, 20, H, STONE[0]);                            // base
  const out = P.smoothEdge(b);                                   // no one-pixel points on the arrow tips
  out.wall = 4;
  return out;
}

// --- Every building of layout v2, by its number in Design/denah-v2 --------------------------------

const NEIGHBOUR_ROOFS = [
  { roofHue: 215, roofSat: 0.28 },                  // blue-grey
  { roofHue: 15, roofSat: 0.32, roofShift: 18 },    // brown-red
  { roofHue: 150, roofSat: 0.3, roofShift: 30 },    // dark green
];

const BUILDINGS = {
  "1":   ["Supa",        () => store(8, 6, { signHue: 355 })],
  "2":   ["Rumah Player", () => house(5, 5, { roofHue: 8, roofSat: 0.42, roofShift: 18 })],
  "3":   ["Konbini",     () => store(7, 5, { signHue: 140, signSat: 0.5, stripe: 25, roofShare: 0.3 })],
  "4":   ["Stasiun",     () => civic(14, 6, { signHue: 210, doorTiles: 3, clock: false })],
  "5":   ["Daiso",       () => store(8, 5, { signHue: 330, signSat: 0.6, roofShare: 0.3 })],
  "6":   ["Rumah Sakit", () => civic(12, 8, { signHue: 200, wallLight: [0.7, 0.9] })],
  "8":   ["Yuubin+ATM",  () => store(7, 5, { signHue: 0, signSat: 0.7, signLight: [0.36, 0.56], roofShare: 0.3 })],
  "10":  ["Apotek",      () => store(4, 4, { signHue: 145, signSat: 0.5, roofShare: 0.3, signH: 6 })],
  "11":  ["Perpus",      () => civic(10, 6, { wallHue: 25, wallSat: 0.22, wallLight: [0.5, 0.7] })],
  "12a": ["SD",          () => civic(14, 8, { clock: true })],
  "12b": ["SMP",         () => civic(11, 7, { clock: true })],
  "12c": ["SMA",         () => civic(11, 7, { clock: true, wallHue: 200, wallSat: 0.06 })],
  "13":  ["Bunga",       () => house(4, 5, { roofShare: 0.34, awning: 330, roofHue: 200, noWindows: true })],
  "14":  ["Restoran",    () => house(6, 5, { roofShare: 0.34, awning: 5, roofHue: 20, roofSat: 0.3, roofShift: 18 })],
  "15":  ["Laundry",     () => store(4, 5, { signHue: 195, signSat: 0.5, roofShare: 0.3, signH: 6 })],
  "16":  ["Drugstore",   () => store(8, 5, { signHue: 215, signSat: 0.55, roofShare: 0.3 })],
  "17":  ["Koban",       () => house(4, 4, { roofShare: 0.36, roofHue: 220, roofSat: 0.18, wallSat: 0.06, lamp: true, noWindows: true })],
  "18":  ["Juntendo",    () => store(12, 7, { signHue: 30, signSat: 0.75, signLight: [0.36, 0.58], roofShare: 0.32, doorTiles: 2 })],
  "19":  ["Sampah",      () => gomi(3, 2)],
  "20":  ["Roti",        () => house(4, 5, { roofShare: 0.34, awning: 35, roofHue: 25, roofSat: 0.3, roofShift: 18, noWindows: true })],
  "21":  ["Daging",      () => house(4, 5, { roofShare: 0.34, awning: 0, awningSat: 0.5, roofHue: 215, noWindows: true })],
  "22":  ["Yaoya",       () => house(4, 5, { roofShare: 0.34, awning: 100, roofHue: 150, roofSat: 0.25, roofShift: 30, noWindows: true })],
  "24":  ["Kue",         () => house(4, 5, { roofShare: 0.34, awning: 345, awningSat: 0.4, roofHue: 15, roofSat: 0.3, roofShift: 18, noWindows: true })],
  "25":  ["Kopi",        () => house(4, 5, { roofShare: 0.34, awning: 25, awningSat: 0.35, roofHue: 215, noWindows: true, wood: true })],
  "23":  ["Shiyakusho",  () => civic(12, 7, { signHue: 215 })],
  "26":  ["Dagashiya",   () => house(5, 5, { awning: 210, awningSat: 0.35, wood: true, roofHue: 215, roofSat: 0.2 })],
};
// Neighbour houses 2b..2k cycle through the three roof colours
["2b", "2c", "2d", "2e", "2f", "2g", "2h", "2i", "2j", "2k"].forEach((no, i) => {
  BUILDINGS[no] = ["Rumah", () => house(5, 5, NEIGHBOUR_ROOFS[i % 3])];
});
BUILDINGS["19b"] = ["Sampah", () => gomi(3, 2)];
// Props from the town layout spots
BUILDINGS["Mailbox"] = ["Kotak pos", mailbox];
BUILDINGS["BusStop"] = ["Halte bus", busStop];
BUILDINGS["ModeBoard"] = ["Papan Mode", signpost];

module.exports = { house, store, civic, gomi, mailbox, busStop, signpost, BUILDINGS };

if (require.main === module) {
  const items = Object.entries(BUILDINGS).filter(([no]) => !/^2[e-k]$|^19b$/.test(no))
    .map(([no, [name, make]]) => ({ buf: make(), label: no + " " + name }));
  if (process.argv[2] === "signpost") {
    const s = signpost();
    const scene = new P.Buf(64, 48); scene.clear([120, 170, 90]);
    for (let y = 0; y < s.h; y++) for (let x = 0; x < s.w; x++) { const c = s.px[y * s.w + x]; if (c >>> 24) scene.px[(y + 8) * scene.w + x + 16] = c; }
    P.save(P.sheet([{ buf: scene, label: "1x", scale: 2 }, { buf: s, label: "6x", scale: 6 }]), path.join(__dirname, "preview_signpost.png"));
    console.log("edge", JSON.stringify(P.edgeCheck(s)).slice(0, 80));
  } else if (process.argv[2] === "export") {
    const out = path.join(__dirname, "../../Assets/Art/Buildings");
    fs.mkdirSync(out, { recursive: true });
    // One PNG per sprite, plus buildings.json with each size and wall height (tiles) for the wall collider
    const meta = [];
    for (const [key, [name, make]] of Object.entries(BUILDINGS)) {
      const buf = make();
      P.save(buf, path.join(out, "Building_" + key + ".png"));
      meta.push({ key, name, w: buf.w / T, h: buf.h / T, wall: Math.round((buf.wall ?? buf.h) / T * 100) / 100 });
    }
    fs.writeFileSync(path.join(out, "buildings.json"), JSON.stringify({ items: meta }, null, 1) + "\n");
    console.log("exported", meta.length, "sprites to", out);
  } else {
    P.save(P.sheet(items, { cols: 6, scale: 2, gap: 8 }), path.join(__dirname, "preview_buildings.png"));
    for (const it of items) {
      const e = P.edgeCheck(it.buf);
      if (e.nubs.length || e.strips.length || e.holes.length) console.log("edge issue:", it.label, e.nubs.length, e.strips.length, e.holes.length);
    }
    console.log("preview:", items.length, "sprites");
  }
}
