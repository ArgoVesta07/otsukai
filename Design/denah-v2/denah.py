import csv, collections, math, sys
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle

OUT = sys.argv[1]
W0, W1, H0, H1 = -48, 48, -32, 32

# (x0, y0, w, h) in tiles; y0 = bottom
ground = [  # kind, rect, label
    ("rail",  (-48, 29, 96, 3), "Rel kereta"),
    ("river", (-48, -32, 96, 3), "Sungai"),
    ("bridge",(-2, -32, 4, 3), ""),
    ("path",  (-48, -29, 96, 2), "Jalan tepi sungai (jalur jalan-jalan)"),
    ("road",  (-48, 16, 96, 3), "Ekimae-dori"),
    ("plaza", (-10, 19, 20, 4), "Ekimae (halte bus, taksi)"),
    ("arcade",(-31, 8, 56, 3), "Shotengai (jalan kaki)"),
    ("road",  (-48, -2, 96, 3), "Naka-dori"),
    ("road",  (-48, -14, 96, 3), "Minami-dori"),
    ("road",  (-2, -29, 4, 45), ""),          # Chuo-dori, station -> bridge
    ("road",  (-34, -27, 3, 43), ""),         # west street
    ("road",  (25, -27, 3, 43), ""),          # east street
    ("park",  (-47, -21, 12, 6), "Taman (jalan-jalan peliharaan)"),
    ("park",  (30, -27, 17, 12), "Taman tepi sungai"),
    ("lot",   (12, -11, 10, 6), "Parkir Supa"),
    ("lot",   (29, -11, 3, 7), ""),
    ("field", (-19, -19, 14, 4), "Lapangan SD"),
    ("hill",  (12, 19, 36, 10), ""),
    ("path",  (14, 19, 34, 2), ""),
]

# No, name, w, h, x0, y0, enterable, category
B = [
 ("4","Stasiun",14,6,-7,23,False,"umum"),
 ("17","Koban",4,4,10,19,False,"umum"),
 ("12c","SMA",11,7,14,21,False,"umum"),
 ("12b","SMP",11,7,29,21,False,"umum"),
 ("2i","Rumah",5,5,-17,19,False,"rumah"),
 ("11","Perpus",10,6,-31,19,True,"umum"),
 ("23","Shiyakusho",12,7,-47,19,True,"umum"),
 # shotengai, west -> east of Chuo-dori
 ("13","Bunga",4,5,-24,11,True,"shotengai"),
 ("24","Kue",4,5,-20,11,True,"shotengai"),
 ("20","Roti",4,5,-16,11,True,"shotengai"),
 ("25","Kopi",4,5,-12,11,True,"shotengai"),
 ("14","Restoran",6,5,-8,11,True,"shotengai"),
 ("21","Daging",4,5,2,11,True,"shotengai"),
 ("22","Yaoya",4,5,6,11,True,"shotengai"),
 ("16","Drugstore",8,5,10,11,True,"toko"),
 # Naka-dori north side
 ("3","Konbini",7,5,-29,1,True,"toko"),
 ("2j","Rumah",5,5,-22,1,False,"rumah"),
 ("8","Yuubin+ATM",7,5,-12,1,True,"umum"),
 ("27b","Papan Mode",2,2,2,1,True,"umum"),   # second ModeSelectPoint board, town centre crossing
 ("5","Daiso",8,5,4,1,True,"toko"),
 ("2k","Rumah",5,5,15,1,False,"rumah"),
 ("6","Rumah Sakit",12,8,30,1,True,"umum"),
 ("10","Apotek",4,4,43,1,True,"toko"),
 # Minami-dori north side
 ("2b","Rumah",5,5,-47,-11,False,"rumah"),
 ("2c","Rumah",5,5,-41,-11,False,"rumah"),
 ("2d","Rumah",5,5,-31,-11,False,"rumah"),
 ("15","Laundry",4,5,-24,-11,True,"toko"),
 ("2h","Rumah",5,5,-19,-11,False,"rumah"),
 ("1","Supa",8,6,4,-11,True,"toko"),
 ("18","Juntendo",12,7,32,-11,True,"toko"),
 # riverside
 ("2e","Rumah",5,5,-47,-27,False,"rumah"),
 ("2f","Rumah",5,5,-41,-27,False,"rumah"),
 ("2","Rumah Player",5,5,-31,-27,True,"rumah"),
 ("19","Sampah",3,2,-25,-27,True,"sampah"),
 ("27","Papan Mode",2,2,-22,-27,True,"umum"),   # ModeSelectPoint board, next to home
 ("12a","SD",14,8,-19,-27,False,"umum"),
 ("26","Dagashiya",5,5,3,-27,True,"toko"),
 ("2g","Rumah",5,5,10,-27,False,"rumah"),
 ("19b","Sampah",3,2,17,-27,True,"sampah"),
]
# NavTarget / ErrandSpot id per building (the ids errand steps use). Buildings with an interior scene
# (a Door) only get a NavTarget, so E isn't shared between the door and an errand spot.
IDS = {"4":"Station","17":"Koban","12c":"HighSchool","12b":"JuniorHigh","3":"Konbini","11":"Library",
       "23":"CityHall","13":"FlowerShop","24":"CakeShop","20":"Bakery","25":"Cafe","14":"Restaurant",
       "21":"Butcher","22":"Greengrocer","16":"Drugstore","8":"PostOffice","5":"Daiso","6":"Hospital",
       "10":"Pharmacy","15":"Laundry","1":"Supermarket","18":"HomeCenter","2":"Home","19":"TrashArea",
       "19b":"TrashAreaEast","12a":"SchoolGate","26":"Dagashiya","27":"ModeBoard","27b":"ModeBoard"}
HAS_INTERIOR = {"1","2","27","27b"}   # no ErrandSpot: their own script (Door / ModeSelectPoint) uses E
# Extra errand spots that aren't building doors: name, id, x, y (tile centre)
SPOTS = [("ATM","ATM",-6.5,0.5),("Taman barat","ParkWest",-41.5,-21.5),("Jembatan","Bridge",-0.5,-27.5),
         ("Taman tepi sungai","ParkRiverside",37.5,-27.5)]
props = [("Kotak pos",-9.5,20.5),("Kotak pos",-4.5,1.5),("Kotak pos",-25.5,-23.5),("Kotak pos",-25.5,11.5),
         ("Halte bus",7.5,20.5),("Halte bus",29.5,0.5)]

def tiles(r):
    x0,y0,w,h = r
    return {(x,y) for x in range(x0,x0+w) for y in range(y0,y0+h)}

# ---- checks
occupied = {}
for b in B:
    for t in tiles(b[4:6]+b[2:4] if False else (b[4],b[5],b[2],b[3])):
        assert W0 <= t[0] < W1 and H0 <= t[1] < H1, ("out of map", b[0])
        assert t not in occupied, ("overlap", b[0], occupied.get(t))
        occupied[t] = b[0]
kind_at = {}
for k,r,_ in ground:
    for t in tiles(r): kind_at[t] = k   # later entries win (bridge over river, roads over park)
for b in B:
    for t in tiles((b[4],b[5],b[2],b[3])):
        assert kind_at.get(t) not in ("road","arcade","plaza","path","river","rail"), ("building on road", b[0], t, kind_at.get(t))
WALK = ("road","arcade","plaza","path","bridge","lot","park","field","hill",None)
def door(b):
    cx = b[4] + b[2]/2
    return (math.floor(cx - 0.01), b[5]-1)
for b in B:
    d = door(b)
    assert kind_at.get(d) in ("road","arcade","plaza","path"), ("door not on a street", b[0], d, kind_at.get(d))
for n,x,y in props:
    assert (math.floor(x),math.floor(y)) not in occupied, ("prop inside building", n)

# ---- walking distance (BFS over everything that isn't a building, river or rail)
def walkable(t):
    return W0 <= t[0] < W1 and H0 <= t[1] < H1 and t not in occupied and kind_at.get(t) in WALK
def bfs(src):
    dist = {src:0}; q = collections.deque([src])
    while q:
        c = q.popleft()
        for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
            n=(c[0]+dx,c[1]+dy)
            if n not in dist and walkable(n): dist[n]=dist[c]+1; q.append(n)
    return dist
home = next(b for b in B if b[0]=="2")
from_home = bfs(door(home))
dist = {b[0]: from_home.get(door(b)) for b in B}
assert all(v is not None for v in dist.values()), dist

# ---- CSV
# Ground areas in tile coordinates (x, y = bottom-left tile), for Otsukai > Town > Paint Ground And Roads.
# Later rows paint over earlier ones.
with open(OUT+"/Denah_Kota_v2_ground.csv","w",newline="") as f:
    w = csv.writer(f)
    w.writerow(["Kind","X","Y","W","H","Label"])
    w.writerow(["ground",W0,H0,W1-W0,H1-H0,"Whole map (Ground tilemap)"])
    for k,(x0,y0,w_,h_),lab in ground:
        w.writerow([k,x0,y0,w_,h_,lab])
with open(OUT+"/Denah_Kota_v2.csv","w",newline="") as f:
    w = csv.writer(f)
    w.writerow(["No","Nama","Ukuran (tile)","Bisa masuk","Posisi root Unity X","Posisi root Unity Y","Pintu X","Pintu Y",
                "Jarak jalan dari rumah (tile)","Target Id","Errand Spot"])
    for b in B:
        d = door(b)
        tid = IDS.get(b[0], "")
        spot = "Ya" if tid and b[0] not in HAS_INTERIOR else "Tidak"
        w.writerow([b[0],b[1],f"{b[2]}x{b[3]}","Ya" if b[6] else "Tidak", b[4]+b[2]/2, b[5], d[0]+0.5, d[1]+0.5,
                    dist[b[0]], tid, spot])
    # spots and mailboxes: size 0x0, root = the spot itself
    extra = SPOTS + [("Kotak pos","Mailbox",x,y) for n,x,y in props if n=="Kotak pos"]
    for i,(name,tid,x,y) in enumerate(extra, 1):
        t = (math.floor(x), math.floor(y))
        assert t not in occupied, ("spot inside a building", name)
        near = min(from_home.get((t[0]+dx,t[1]+dy), 10**9) for dx in (-1,0,1) for dy in (-1,0,1))
        assert near < 10**9, ("spot not reachable", name)
        w.writerow([f"T{i}",name,"0x0","Tidak",x,y,x,y,near,tid,"Ya"])

# ---- picture
col = {"rail":"#7d7068","river":"#7fb2dc","bridge":"#a9845c","path":"#e2cfa9","road":"#b9b5ad","plaza":"#d9cfc3",
       "arcade":"#e3b98d","park":"#6fa65c","lot":"#a3a3a3","field":"#dcc08e","hill":"#9cc583"}
bc = {"rumah":"#f1c46d","toko":"#e8896a","shotengai":"#e7a1c4","umum":"#91aee0","sampah":"#4f7d4f"}
fig, ax = plt.subplots(figsize=(19.2,11.2), dpi=100)
ax.add_patch(Rectangle((W0,H0),96,64,color="#97c46d",zorder=0))
for k,(x0,y0,w_,h_),lab in ground:
    ax.add_patch(Rectangle((x0,y0),w_,h_,color=col[k],zorder=1))
    if lab: ax.text(x0+w_/2,y0+h_/2,lab,ha="center",va="center",fontsize=7,color="#3a3a3a",zorder=2)
for x in range(W0,W1+1): ax.plot([x,x],[H0,H1],color="#00000014",lw=0.4,zorder=2)
for y in range(H0,H1+1): ax.plot([W0,W1],[y,y],color="#00000014",lw=0.4,zorder=2)
for b in B:
    no,name,w_,h_,x0,y0,ent,cat = b
    ax.add_patch(Rectangle((x0,y0),w_,h_,facecolor=bc[cat],edgecolor="#333",lw=1.4,zorder=3))
    ax.text(x0+w_/2,y0+h_/2+0.4,no,ha="center",va="center",fontsize=8,fontweight="bold",zorder=4)
    if w_>=5: ax.text(x0+w_/2,y0+h_/2-0.9,name,ha="center",va="center",fontsize=6.5,zorder=4)
    d=door(b); ax.add_patch(Rectangle((x0+w_/2-0.75,y0-0.25),1.5,0.5,color="#6b4423" if ent else "#9a9a9a",zorder=4))
    if ent and no!="2":
        ax.text(x0+w_/2,y0+h_+0.45,f"{dist[no]}",ha="center",va="center",fontsize=6,color="#1d4f91",zorder=4)
for n,x,y in props:
    ax.add_patch(Rectangle((x-0.5,y-0.5),1,1,color="#d64545" if n=="Kotak pos" else "#2f6db5",ec="#333",lw=0.6,zorder=5))
ax.set_xlim(W0,W1); ax.set_ylim(H0,H1); ax.set_aspect("equal")
ax.set_xticks(range(W0,W1+1,8)); ax.set_yticks(range(H0,H1+1,8)); ax.tick_params(labelsize=7)
ax.set_title("Denah kota Otsukai v2 · 96x64 tile · angka biru = jarak jalan kaki dari pintu rumah player (tile)",fontsize=10,loc="left")
import matplotlib.patches as mp
handles=[mp.Patch(color=c,label=l) for l,c in [("Rumah","#f1c46d"),("Toko","#e8896a"),("Shotengai","#e7a1c4"),("Fasilitas umum","#91aee0"),("Tempat sampah","#4f7d4f"),("Kotak pos","#d64545"),("Halte bus","#2f6db5"),("Pintu (bisa masuk)","#6b4423"),("Pintu (dekor)","#9a9a9a")]]
ax.legend(handles=handles,loc="upper left",bbox_to_anchor=(1.01,1),fontsize=8,frameon=False)
plt.tight_layout(); plt.savefig(OUT+"/Denah_Kota_v2.png")
for b in sorted(B,key=lambda b: dist[b[0]]):
    if b[6] or b[0] in ("12a","4"): print(f"{b[0]:>4} {b[1]:<13} {dist[b[0]]:>3} tile  ~{dist[b[0]]/5:4.1f}s @speed5")

routes = [
 ("1 Balikin buku perpus", ["2","11","2"]),
 ("2 Beli bunga", ["2","13","2"]),
 ("3 Print di konbini", ["2","3","2"]),
 ("4 Nyuci di laundry", ["2","15","2"]),
 ("5 Anter paket ke yuubin", ["2","8","2"]),
 ("6 Temenin ortu ke RS + apotek", ["2","6","10","2"]),
 ("7 Bayar tagihan di konbini", ["2","3","2"]),
 ("8 Buang sampah", ["2","19","2"]),
 ("9 Ambil uang ATM lalu beli pupuk", ["2","8","18","2"]),
 ("10 Roti kesukaan ibu", ["2","20","2"]),
 ("11 Beli daging", ["2","21","2"]),
 ("12 Ambil uang ATM", ["2","8","2"]),
 ("13 Ke Daiso", ["2","5","2"]),
 ("14 Jalan-jalan peliharaan (loop)", ["2","19b","2"]),
 ("15 Anter adek ke dagashiya", ["2","26","2"]),
 ("16 Anter adek ke SD", ["2","12a","2"]),
 ("17 Temenin bapak ke shiyakusho", ["2","23","2"]),
 ("Belanja shotengai: roti+daging+yaoya", ["2","20","21","22","2"]),
]
byno = {b[0]:b for b in B}
cache = {}
def leg(a,b):
    if a not in cache: cache[a]=bfs(door(byno[a]))
    return cache[a][door(byno[b])]
print()
for name, stops in routes:
    d = sum(leg(stops[i],stops[i+1]) for i in range(len(stops)-1))
    print(f"{name:<40} {d:>4} tile ~{d/5:5.1f}s")
