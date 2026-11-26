# bus_gen.py -> vehicles.json (vehicles.html için üçgenler + malzemeler). Kullanım:
#   python3 Tools/vehicles/export_json.py [--low] [MODEL ...] [çıktı.json]
import json, math, os, sys
G = {"math": math}
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "bus_gen.py")).read(), G)
specs = G["vehicle_specs"]()
out = {}
only = [a for a in sys.argv[1:] if not a.endswith(".json") and a != "--low"] or list(specs)
if "--low" in sys.argv: G["Q"]["low"] = True
for vid in only:
    spec = specs[vid]
    M = G["build_vehicle"](spec)
    mats = G["material_table"](spec["color"])
    parts = []
    npoly = 0
    for name in M["order"]:
        p = M["parts"][name]
        # parca + malzeme ikilisine gore grupla
        tris = []
        for poly in p["polys"]:
            npoly += 1
            for i in range(1, len(poly) - 1):
                for q in (poly[0], poly[i], poly[i + 1]):
                    tris.extend([round(q[0], 4), round(q[1], 4), round(q[2], 4)])
        parts.append({"name": name, "mat": p["mat"], "pos": tris})
    used = sorted(set(pp["mat"] for pp in parts))
    out[vid] = {"L": spec["L"], "parts": parts, "materials": {m: {"rgb": mats[m][0], "kind": mats[m][1]} for m in used}}
    print(vid, "polys", npoly, "parts", len(parts))
json.dump(out, open(sys.argv[-1] if sys.argv[-1].endswith(".json") else "vehicles.json", "w"))
