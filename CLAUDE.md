# Uno Associations — Project Rules

## Code Style for Scripts

- **Unity 6, C# 9.0.** No file-scoped namespaces.
- **No underscore prefix** on private fields. Use `layout`, not `_layout`.
- **No alignment spaces.** Don't pad variable names with extra spaces to form columns.
- **No comments unless the WHY is non-obvious** — a hidden constraint, a workaround for a specific bug, or a subtle invariant. Never explain what the code does.
- **No multi-line comment blocks or docstrings.**
- `[SerializeField]` for inspector-exposed fields. All other fields are private.
- **Static events** for cross-component communication (e.g. `Card.Dropped`, `HandManager.CardLeftHand`). Subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- **DOTween** for animations. **Unity Splines** (SplineContainer, BezierKnot) for hand card layout.
- **New Input System** via Canvas `EventSystem` (`IPointerDownHandler`, `IDragHandler`, `IPointerUpHandler`) — never poll `Input` directly.
- **TextMesh Pro** (`TMP_Text`) for all in-game text.
- **JsonUtility** cannot deserialize bare JSON arrays. Levels are wrapped as `{"items":[...]}` in `LevelLoader.LoadLevels`.
- When a constructor or method parameter name clashes with a field name, use `this.fieldName = paramName`.
- `ICardDrop.OnCardDrop` returns `bool` — `true` = accepted, `false` = snap back.

---

## Level Data Pipeline

Levels are authored in **`Assets/Resources/db.xlsx`** (two sheets: `Levels` and `Connections`) and compiled into self-contained `Assets/Resources/Data/Levels/level_N.json` files by **`import_levels.py`** (run from the project root: `python3 import_levels.py`). There is no shared `cards.json` / `tags.json` / `tag-rules.json` / `connections.json` anymore — each level JSON embeds its own card list and its own valid connection pairs, and the game only ever reads within the currently loaded level.

### Editing levels
1. Edit `db.xlsx` — `Levels` sheet row: `Level, Difficulty, Moves, Start_Card, Open_Cards_Count, Open_Cards, Deck_Cards_Count, Deck_Cards, Minimum_Moves_Required, ...`. `Connections` sheet row (same `Level` number): `Connection_Pairs_Count, Connectable_Word_Pairs` (`"A <> B | C <> D | ..."`).
2. Run `python3 import_levels.py` from the project root. It regenerates every `level_N.json` from the sheet — always re-run it after editing `db.xlsx`, don't hand-edit the generated JSON.
3. Card names become lowercase ids (`Emergency_Room` → `emergency_room`); display text keeps underscores as spaces (`Emergency Room`); `image` defaults to the id, so sprites must be named to match.

### Level JSON format (generated, one file per level)
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
- `cards` — every card used by this level (`activeCard` + `hand` + `deck`), with the id/text/image the game needs to render it. Not shared with other levels.
- `connections` — every valid card-to-card pair for this level only. `ActiveCardSlot.OnCardDrop` checks this via a per-level `ConnectionGraph` built fresh in `GameController.LoadLevel`.
- A level file is still a **JSON array of variants** — the loader picks one at random on load/restart — but `import_levels.py` currently emits exactly one variant per level (one row per level in the sheet). Hand-added extra variants are fine as long as they reuse the same `cards`/`connections`.
- `maxMoves` / `optimalMoves` drive `MoveCounter` and star scoring (`HudStarDisplay`) directly — no multiplier is applied in code.
