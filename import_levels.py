#!/usr/bin/env python3
"""
Imports level data from Assets/Resources/db.xlsx into Assets/Resources/Data/Levels/level_N.json.

Run from the project root: python3 import_levels.py

db.xlsx has two sheets:
  Levels      — Level, Difficulty, Moves, Start_Card, Open_Cards, Deck_Cards,
                Minimum_Moves_Required, ... (columns beyond these are ignored)
  Connections — Level, Connection_Pairs_Count, Connectable_Word_Pairs
                ("A <> B | C <> D | ...")

Each level_N.json is fully self-contained: it embeds its own card definitions
(id/text/image) and its own valid connection pairs, so the game no longer needs
a shared cards.json / tag-rules.json / connections.json at runtime. Re-run this
script any time db.xlsx changes to regenerate the level files.
"""

import json, re, sys, zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT       = Path(__file__).parent
XLSX_PATH  = ROOT / "Assets/Resources/db.xlsx"
LEVELS_DIR = ROOT / "Assets/Resources/Data/Levels"

NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}


# ─── Minimal xlsx reader (stdlib only) ───────────────────────────────────────

def col_to_idx(cell_ref):
    letters = re.match(r'([A-Z]+)', cell_ref).group(1)
    idx = 0
    for c in letters:
        idx = idx * 26 + (ord(c) - ord('A') + 1)
    return idx - 1


def load_shared_strings(z):
    try:
        data = z.read('xl/sharedStrings.xml')
    except KeyError:
        return []
    root = ET.fromstring(data)
    return [
        ''.join(t.text or '' for t in si.iter('{%s}t' % NS['s']))
        for si in root.findall('s:si', NS)
    ]


def parse_sheet(z, sheet_path, shared_strings):
    root = ET.fromstring(z.read(sheet_path))
    rows = []
    for row in root.iter('{%s}row' % NS['s']):
        row_data = {}
        max_idx = -1
        for c in row.findall('s:c', NS):
            idx = col_to_idx(c.get('r'))
            max_idx = max(max_idx, idx)
            t = c.get('t')
            v_el, is_el = c.find('s:v', NS), c.find('s:is', NS)
            if is_el is not None:
                value = ''.join(t2.text or '' for t2 in is_el.iter('{%s}t' % NS['s']))
            elif v_el is not None:
                raw = v_el.text or ''
                value = shared_strings[int(raw)] if t == 's' else raw
            else:
                value = ''
            row_data[idx] = value
        if max_idx >= 0:
            rows.append([row_data.get(i, '') for i in range(max_idx + 1)])
    return rows


def read_sheets(path):
    z = zipfile.ZipFile(path)
    shared_strings = load_shared_strings(z)
    wb = ET.fromstring(z.read('xl/workbook.xml'))
    names = [el.get('name') for el in wb.findall('.//s:sheet', NS)]
    files = sorted(n for n in z.namelist() if n.startswith('xl/worksheets/sheet') and n.endswith('.xml'))
    return {name: parse_sheet(z, f, shared_strings) for name, f in zip(names, files)}


def rows_to_dicts(rows):
    header = rows[0]
    out = []
    for row in rows[1:]:
        if not row or not row[0].strip():
            continue
        out.append({header[i]: (row[i] if i < len(row) else '') for i in range(len(header))})
    return out


# ─── Domain parsing ───────────────────────────────────────────────────────────

def to_id(name):
    return name.strip().lower()


def to_text(name):
    return name.strip().replace('_', ' ')


def split_names(cell):
    return [p.strip() for p in cell.split(',') if p.strip()]


def make_card(name):
    return {"id": to_id(name), "text": to_text(name), "image": to_id(name)}


def parse_connections(cell):
    pairs = []
    for part in cell.split('|'):
        part = part.strip()
        if not part:
            continue
        a, b = part.split('<>')
        pairs.append({"card1": to_id(a), "card2": to_id(b)})
    return pairs


# ─── JSON formatting (collapse primitive arrays onto one line) ──────────────

def format_level_json(data):
    s = json.dumps(data, indent=2, ensure_ascii=False)
    def collapse(m):
        inner = re.sub(r'\s+', ' ', m.group(1).strip())
        return '[' + inner + ']'
    return re.sub(r'\[\s*((?:[^\[\]{}])*?)\s*\]', collapse, s, flags=re.DOTALL)


# ─── Main ─────────────────────────────────────────────────────────────────────

def main():
    if not XLSX_PATH.exists():
        print(f"Missing {XLSX_PATH}")
        sys.exit(1)

    sheets = read_sheets(XLSX_PATH)
    levels_by_num = {int(float(r["Level"])): r for r in rows_to_dicts(sheets["Levels"])}
    conns_by_num  = {int(float(r["Level"])): r for r in rows_to_dicts(sheets["Connections"])}

    LEVELS_DIR.mkdir(parents=True, exist_ok=True)
    written = 0

    for num in sorted(levels_by_num):
        row = levels_by_num[num]
        conn_row = conns_by_num.get(num)
        if conn_row is None:
            print(f"  Level {num}: no matching Connections row, skipping.")
            continue

        active_name = row["Start_Card"].strip()
        hand_names   = split_names(row["Open_Cards"])
        deck_names   = split_names(row["Deck_Cards"])

        all_names = [active_name] + hand_names + deck_names
        seen = set()
        cards = []
        for n in all_names:
            cid = to_id(n)
            if cid in seen:
                continue
            seen.add(cid)
            cards.append(make_card(n))

        variant = {
            "activeCard":   to_id(active_name),
            "hand":         [to_id(n) for n in hand_names],
            "deck":         [to_id(n) for n in deck_names],
            "maxMoves":     int(float(row["Moves"])),
            "optimalMoves": int(float(row["Minimum_Moves_Required"])),
            "cards":        cards,
            "connections":  parse_connections(conn_row["Connectable_Word_Pairs"]),
            "consumableFreeUses": [
                {"id": "wildCard", "freeUses": int(float(row["Free_Wild"])), "cost": int(float(row["Wild_Cost"]))},
                {"id": "undo",     "freeUses": int(float(row["Free_Undo"])), "cost": int(float(row["Undo_Cost"]))},
            ],
        }

        out_path = LEVELS_DIR / f"level_{num}.json"
        out_path.write_text(format_level_json([variant]), encoding="utf-8")
        print(f"  Wrote {out_path.name}  (active={variant['activeCard']}, "
              f"hand={len(variant['hand'])}, deck={len(variant['deck'])}, "
              f"connections={len(variant['connections'])})")
        written += 1

    print(f"\n{written} level file(s) written to {LEVELS_DIR}")


if __name__ == "__main__":
    main()
