# Game Data — Structure & Reference

This folder contains the compiled level data that drives the game. Every level is fully self-contained: it carries its own card list and its own valid card-to-card connections, so the game never needs a shared card/tag database at runtime.

---

## Where levels come from

Levels are authored in **`Assets/Resources/db.xlsx`** (two sheets, `Levels` and `Connections`) and compiled into this folder by **`import_levels.py`** (project root):

```
db.xlsx  ──▶  python3 import_levels.py  ──▶  Data/Levels/level_1.json, level_2.json, …
```

To change a level: edit `db.xlsx`, then re-run `python3 import_levels.py` from the project root. It regenerates every `level_N.json` from scratch — don't hand-edit the generated files, edit the spreadsheet instead.

`db.xlsx` sheets:
- **`Levels`** — one row per level: `Level, Difficulty, Moves, Start_Card, Open_Cards_Count, Open_Cards, Deck_Cards_Count, Deck_Cards, Minimum_Moves_Required, Possible_Solutions, Solution_Orders, Notes`. Only `Level`, `Start_Card`, `Open_Cards`, `Deck_Cards`, `Moves`, and `Minimum_Moves_Required` are consumed by the importer.
- **`Connections`** — one row per level (same `Level` number): `Level, Connection_Pairs_Count, Connectable_Word_Pairs`, where `Connectable_Word_Pairs` is `"A <> B | C <> D | ..."`.

---

## `Levels/level_1.json`, `level_2.json`, …

Each level lives in its own file and contains an **array of variants**. When the level loads, one variant is picked at random. `import_levels.py` currently writes exactly one variant per level (one spreadsheet row per level).

```json
[
  {
    "activeCard": "pond",
    "hand": ["fish", "cat", "milk"],
    "deck": [],
    "maxMoves": 5,
    "optimalMoves": 3,
    "cards": [
      {"id": "pond", "text": "Pond", "image": "pond"},
      {"id": "fish", "text": "Fish", "image": "fish"},
      {"id": "cat", "text": "Cat", "image": "cat"},
      {"id": "milk", "text": "Milk", "image": "milk"}
    ],
    "connections": [
      {"card1": "pond", "card2": "fish"},
      {"card1": "fish", "card2": "cat"},
      {"card1": "cat", "card2": "milk"}
    ]
  }
]
```

| Field          | Purpose |
|----------------|---------|
| `activeCard`   | The card that starts face-up in the centre slot (an `id` from `cards`). |
| `hand`         | Cards dealt to the player's hand at the start. |
| `deck`         | Remaining draw pile (top → bottom order). |
| `maxMoves`     | Move budget shown in the HUD and used by `MoveCounter`. |
| `optimalMoves` | Minimum moves to solve — used by `HudStarDisplay` for star scoring. |
| `cards`        | Every card this level uses (`activeCard` + `hand` + `deck`), each with `id`, `text` (display name), and `image` (sprite name — defaults to `id`, so artwork must be named to match). |
| `connections`  | Every valid `card1 ↔ card2` pair for this level. Symmetric — listing a pair once covers both directions. This is the only source of truth for what the player can drop where; `ActiveCardSlot` checks a `ConnectionGraph` built from this list fresh each time a level loads. |
| `consumableFreeUses` (optional) | Per-consumable free-use counts, e.g. `[{"id": "wildCard", "freeUses": 10}]`. Omitted → 0 free uses. |

Cards, hand, deck, and connections are **scoped to the level** — the same card id (e.g. `"milk"`) can mean different things (different text/image) across two different levels, since nothing is shared.

---

## Level design rules

1. **Hand must not be in solve order.** If the optimal chain is A→B→C, the hand should not contain them in that sequence. The player should have to figure out the order.
2. **Deck order matters.** The deck is drawn top-to-bottom (index 0 first). Place cards in the order the player should naturally draw them along the optimal path.
3. **`optimalMoves` must match the true minimum.** If the fastest possible win needs a certain number of moves, `Minimum_Moves_Required` in the sheet (→ `optimalMoves`) must reflect that exactly, since it drives star scoring.
4. **Verify every variant by tracing the chain.** Step through the full optimal path in `Connectable_Word_Pairs` and confirm it reaches an empty hand using only listed connections.
