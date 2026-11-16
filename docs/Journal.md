# Journal System

A two-section journal (**Notes** and **Items**). Each section opens on a **contents page** that lists only the entries the player has picked up. Clicking a link opens that entry's **page**. Bookmark buttons switch between sections. A new entry carries a **"!"** marker until the player opens its page for the first time.

All scripts are in `Scripts/Journal/` under the namespace `SteelAndSalt.Journal`.

---

## 1. The big picture

The system has three layers. Each layer only talks to the one below it.

```
 ┌───────────────────────── UI (shows things) ─────────────────────────┐
 │  JournalUI  ──owns──►  JournalBookmark × N                          │
 │      │ reads data, calls MarkRead      ▲ listens to signals          │
 └──────┼─────────────────────────────────┼─────────────────────────────┘
        ▼                                 │
 ┌────────────────────── State (remembers things) ─────────────────────┐
 │  JournalManager (autoload "Journal")                                 │
 │      list of owned entries + set of unread entries                   │
 └──────▲───────────────────────────────────────────────────────────────┘
        │ Add(entry)
 ┌──────┴──────────────── World (adds things) ─────────────────────────┐
 │  JournalPickup (an Interactable on a 3D object)                      │
 └──────────────────────────────────────────────────────────────────────┘

 Data used by all of them:  JournalEntry (.tres resource)
```

**The rule that keeps it simple:** the state (`JournalManager`) never touches the UI. When something changes, it emits a **signal**, and anything interested (the journal UI, a HUD icon, a sound player, and so on) reacts to it. You can add new listeners without editing the manager.

### What happens when the player picks up a letter

```
1. Player looks at the letter, presses E
   Interactor → JournalPickup.Interact() → OnInteract()
2. OnInteract → JournalManager.Add(letter)
   letter is stored, marked unread
   signals: EntryAdded(letter), UnreadChanged
3. JournalUI.RefreshBookmarks() runs (it listens to UnreadChanged)
   → Notes bookmark shows "!"
4. JournalPickup removes the letter object from the world
5. Player presses J → JournalUI.Toggle() → Open() → ShowContents(Notes)
   → contents page shows  "! 1. Letter"
6. Player clicks the link → ShowEntry(letter)
   → page is filled in, JournalManager.MarkRead(letter)
   → signals: EntryRead, UnreadChanged → "!" disappears from the bookmark
   → next time contents is rebuilt, the row has no "!" either
```

---

## 2. Setup checklist

1. **Autoload:** Project Settings → Globals → Autoload has `Journal` → `res://Scripts/Journal/JournalManager.cs`. It's already added in `project.godot`.
2. **Input action:** `journal_toggle` exists in the Input Map (J). The name must match `JournalUI.ToggleAction`.
3. **Journal scene:** placed in the level or HUD, with all `JournalUI` exports assigned.
4. **Entries:** one `.tres` file per note or item.
5. **Pickups:** a `JournalPickup` (Area3D + CollisionShape3D) on each world object, with `Entry` set.

---

## 3. `JournalEntry` — data (Resource)

`JournalEntry.cs` · `[GlobalClass] partial class JournalEntry : Resource`

A plain data container with no logic. `[GlobalClass]` makes it appear in **New Resource**, so you create entries in the editor: right-click in FileSystem → New Resource → JournalEntry → save as `.tres`.

### `enum JournalSection`
| Value | Meaning |
|---|---|
| `Notes` (0) | letters, notes, documents |
| `Items` (1) | objects |

The enum decides which section an entry appears in and which bookmark it belongs to. To add a section, see §9.

### Properties
| Property | Type | Purpose |
|---|---|---|
| `Id` | string | Unique key. Leave it empty to use the file path as the key. Set it if you want a stable name like `"letter_captain"` that survives moving files. |
| `Section` | JournalSection | Which section the entry belongs to. |
| `Title` | string | Shown in the contents list and as the page header. |
| `Description` | string (multiline) | Page text. Supports **BBCode** (`[b]bold[/b]`, `[i]italic[/i]`, `[color=red]…[/color]`). |
| `Icon` | Texture2D | Small picture next to the title in the contents list. |
| `Illustration` | Texture2D | Big picture on the entry page. For **Items**, if this is empty, `Icon` is used instead. |
| `SortOrder` | int | Position in the contents list (lower comes first). Entries with equal values stay in pickup order, so if you leave everything at 0, the list follows pickup order. |
| `Key` | string (read-only, not exported) | `Id` if set, otherwise `ResourcePath`. **The manager uses this to identify entries.** |

> ⚠ **Always save entries as separate `.tres` files.** An entry created inline inside a scene has no stable path, so saving and loading can't find it again.

---

## 4. `JournalManager` — state (autoload)

`JournalManager.cs` · `partial class JournalManager : Node`

Registered as the autoload **`Journal`**: Godot creates one instance at game start, and it survives scene changes. Reach it from any script with `JournalManager.Instance`. (From GDScript, use `get_node("/root/Journal")`.)

### Fields (private)
| Field | Type | Purpose |
|---|---|---|
| `_entries` | `List<JournalEntry>` | Every owned entry, in **pickup order**. |
| `_byKey` | `Dictionary<string, JournalEntry>` | Key → entry, for fast "do I have this?" checks. |
| `_unread` | `HashSet<string>` | Keys of entries the player hasn't opened yet. |

### Signals
| Signal | When it fires | Who uses it |
|---|---|---|
| `EntryAdded(JournalEntry entry)` | a new entry was added | `JournalUI.OnEntryAdded`; add a "New note!" popup here |
| `EntryRead(JournalEntry entry)` | an entry was opened for the first time | free to use (sound, achievements…) |
| `UnreadChanged()` | the set of unread entries changed (add, read, load, clear) | `JournalUI.RefreshBookmarks`; add a HUD "!" here |
| `Reloaded()` | everything was replaced (`Load` / `Clear`) | `JournalUI.OnReloaded` |

Subscribe from C# like this: `JournalManager.Instance.UnreadChanged += MyMethod;`. Unsubscribe in `_ExitTree` with `-=`, because the manager lives longer than most scenes.

### Properties
| Member | Purpose |
|---|---|
| `static Instance` | The single instance. It's `null` before the autoload exists or if it's missing. |
| `HasAnyUnread` | `true` if any entry in any section is unread. Handy for a HUD marker. |

### Functions

#### `_EnterTree()`
Godot calls this when the node enters the scene tree. It sets `Instance = this`. If an instance already exists (for example, the script was also placed in a scene by mistake), the duplicate deletes itself, so only one manager ever exists.

#### `_ExitTree()`
Clears `Instance` if this node was the instance, so no one keeps a reference to a freed node.

#### `bool Add(JournalEntry entry)`
Adds an entry to the journal.
- Returns `false` and does nothing if `entry` is null, has no key (no `Id` and no path, which also logs an error), or is **already owned**.
- Otherwise it appends the entry to `_entries`, stores it in `_byKey`, **marks it unread**, emits `EntryAdded` then `UnreadChanged`, and returns `true`.

You can call it from anywhere, not only from pickups: quest rewards, dialogue, cutscenes.
```csharp
JournalManager.Instance.Add(GD.Load<JournalEntry>("res://Journal/letter_01.tres"));
```

#### `bool Has(JournalEntry entry)` / `bool Has(string key)`
Checks whether the player owns this entry. The string version takes an `Id` (or a path). Use it for quest conditions, such as "door opens if player has the key note".

#### `bool IsUnread(JournalEntry entry)`
Returns `true` if the entry is owned and hasn't been opened yet. The UI uses it to draw "!" on a contents row.

#### `bool HasUnread(JournalSection section)`
Returns `true` if any owned entry in that section is unread. The UI uses it for the bookmark's "!".

#### `void MarkRead(JournalEntry entry)`
Removes the entry from `_unread`. If it was actually unread, it emits `EntryRead` and `UnreadChanged`; otherwise it does nothing. `JournalUI.ShowEntry` calls it, which is what "opening the description removes the !" means.

#### `List<JournalEntry> GetEntries(JournalSection section)`
Returns the owned entries of one section, in contents order: sorted by `SortOrder`, with ties kept in pickup order (LINQ `OrderBy` is *stable*). It returns a **new list** each call, so changing the list doesn't change the journal.

#### `void Clear()`
Empties everything, for example for a new game. Emits `Reloaded` and `UnreadChanged`.

#### `Godot.Collections.Dictionary Save()`
Returns the state as a dictionary you can put in your save file:
```
{ "entries": ["res://…/a.tres", "res://…/b.tres"],   // owned, in pickup order
  "unread":  ["res://…/b.tres"] }                      // still unread
```
It stores **file paths**, which is why entries must be `.tres` files.

#### `void Load(Godot.Collections.Dictionary data)`
The reverse of `Save`. It clears the current state, loads every path with `ResourceLoader`, and restores the unread markers. Paths that no longer exist (a deleted or renamed file) are skipped with a warning. Emits `Reloaded` and `UnreadChanged`.

Example with a save file:
```csharp
// save
var file = FileAccess.Open("user://save.json", FileAccess.ModeFlags.Write);
file.StoreString(Json.Stringify(JournalManager.Instance.Save()));

// load
var text = FileAccess.GetFileAsString("user://save.json");
JournalManager.Instance.Load(Json.ParseString(text).AsGodotDictionary());
```

---

## 5. `JournalPickup` — adding entries from the world

`JournalPickup.cs` · `[GlobalClass] partial class JournalPickup : Interactable`

Inherits from your `Interactable` (an `Area3D`), so the existing `Interactor` raycast already detects it, shows the prompt and calls `Interact()`. The class only adds what happens on interaction.

### Typical object setup
```
Letter (StaticBody3D or Node3D)
├─ MeshInstance3D
├─ CollisionShape3D            (for physics, optional)
└─ JournalPickup (Area3D)      ← Entry = letter_01.tres
   └─ CollisionShape3D         ← what the interaction ray hits
```

### Signal
| Signal | When |
|---|---|
| `PickedUp(JournalEntry entry)` | right after the entry is added; use it for a sound or animation |

(It also inherits `Interacted` and `FocusChanged` from `Interactable`.)

### Exports
| Export | Purpose |
|---|---|
| `Entry` | The `.tres` this object gives. **Required.** |
| `RemoveOnPickup` | The node to delete after pickup. If empty, the **parent** is deleted (the whole letter object). If the parent is the level root itself, only this node is deleted, so you never delete the level by accident. |
| `KeepInWorld` | `true` keeps the object in the world (a notice board you can "copy" into the journal, for example). |
| *(inherited)* | `PromptText`, `Icon`, `HoldDuration`, `Trigger`, `Cooldown`, `MaxDistance`… from `Interactable`. |

### Functions

#### `JournalPickup()` (constructor)
Sets the defaults `Type = PickUp` and `SingleUse = true`, so new pickups show "Подобрать" and can't be used twice. You can still change them in the Inspector.

#### `_Ready()`
When the level loads, if the player **already owns** this entry (after loading a save), it removes the object right away, so the letter doesn't reappear. It's skipped when `KeepInWorld` is on.

> Order matters: load the save **before** the level scene is instanced, or this check runs while the journal is still empty.

#### `OnInteract(in InteractionContext context)` (override)
Called by `Interactable.Interact()` after its checks (enabled, distance, cooldown) pass.
1. If `Entry` or the manager is missing, it logs an error and returns `Failed`.
2. Calls `JournalManager.Instance.Add(Entry)`.
3. Emits `PickedUp`.
4. Unless `KeepInWorld` is on, calls `RemoveFromWorld()`.
5. Returns `Success`, so `Interactable` marks it used and emits `Interacted`.

#### `RemoveFromWorld()` (private)
Picks the node to delete (`RemoveOnPickup`, else the parent, else itself) and calls `QueueFree()`. The node is freed safely at the end of the frame.

---

## 6. `JournalUI` — the journal window

`JournalUI.cs` · `[GlobalClass] partial class JournalUI : Control`

Shows the journal and handles all clicks. It **holds no game data**: everything comes from `JournalManager`, so you can close it, delete it or rebuild it without losing progress.

It shows one of two pages at a time:
- **Contents page** (`CurrentEntry == null`): the list of links for `CurrentSection`.
- **Entry page** (`CurrentEntry != null`): the text and picture of one entry.

The same two pages are reused for both sections; only their content changes.

### Signals
| Signal | When |
|---|---|
| `Opened()` | the window became visible; use it for a page-flip sound or to hide the HUD |
| `Closed()` | the window was hidden |

### Exports

**Behaviour**
| Export | Purpose |
|---|---|
| `ToggleAction` | Input Map action that opens and closes the journal (`journal_toggle`). |
| `PauseGame` | If on, `GetTree().Paused = true` while the journal is open. The player, enemies and the `Interactor` stop. |
| `StartSection` | The section shown the first time the journal is opened. |

**Bookmarks**
| Export | Purpose |
|---|---|
| `BookmarkContainer` | The node whose **direct children** are `JournalBookmark`s. Any bookmark you add there is picked up automatically. |

**Contents Page**
| Export | Node type | Purpose |
|---|---|---|
| `ContentsPage` | Control | Root of the contents page (shown and hidden as a whole). |
| `ContentsTitle` | Label | Gets "Записки" / "Предметы". |
| `ContentsList` | Container (VBoxContainer) | Rows are created **inside** it at runtime. Leave it empty in the editor. |
| `ContentsEmptyLabel` | Control | Shown when the section has no entries yet. |

**Entry Page**
| Export | Node type | Purpose |
|---|---|---|
| `EntryPage` | Control | Root of the entry page. |
| `EntryTitle` | Label | Entry title. |
| `EntryIllustration` | TextureRect | Picture, hidden when there is none. |
| `EntryText` | RichTextLabel | Description. **Turn on `BBCode Enabled`** for formatting. |
| `EntryScroll` | ScrollContainer | Scrolled back to the top on every new page. |
| `BackButton` | Button | Back to contents. |
| `PrevButton` / `NextButton` | Button | Flip to the previous or next entry of the section. |
| `PageNumber` | Label | Shows "2 / 5". |

**Texts**
| Export | Purpose |
|---|---|
| `NotesTitle`, `ItemsTitle` | Contents headers per section. |
| `UnreadMarker` | The marker text (`"!"`). |
| `UnreadColor` | The marker colour in the contents list. |

Every export is null-checked (`if (X is not null)`), so you can leave out parts you don't want, such as page numbers or Prev/Next.

### Public state
| Member | Meaning |
|---|---|
| `IsOpen` | Same as `Visible`. |
| `CurrentSection` | The section being shown (or last shown). |
| `CurrentEntry` | The open entry, or `null` when the contents page is shown. |

### Private fields
| Field | Purpose |
|---|---|
| `_bookmarks` | Bookmarks found in `BookmarkContainer` at start. |
| `_journal` | Cached `JournalManager.Instance`. |
| `_prevMouseMode`, `_prevPaused` | What the mouse mode and pause state were before opening, so `Close()` restores them exactly. |

### Functions — lifecycle

#### `_Ready()`
Runs once when the node enters the scene.
1. `ProcessMode = Always`: the journal keeps working while the game is paused. Without this, its buttons and input would freeze together with the game.
2. Hides itself and sets `CurrentSection = StartSection`.
3. Goes through `BookmarkContainer`'s children, stores every `JournalBookmark` and connects its `Pressed` signal to `ShowContents(bookmark.Section)`.
4. Connects `BackButton` → `ShowContents`, `PrevButton` → `Flip(-1)` and `NextButton` → `Flip(+1)`.
5. Gets the manager and subscribes to `EntryAdded`, `UnreadChanged` and `Reloaded`. It logs an error if the autoload is missing.
6. Draws the bookmarks' initial "!".

#### `_ExitTree()`
Unsubscribes from the manager's signals. The manager outlives this UI, and without unsubscribing it would call methods on a deleted node after a scene change.

#### `_UnhandledInput(InputEvent @event)`
Godot calls this for input that no GUI element used.
- `ToggleAction` pressed → `Toggle()`.
- `ui_cancel` (Esc) pressed while open → `Close()`.

`SetInputAsHandled()` stops the key from also reaching other scripts, such as a pause menu also opening on Esc.

### Functions — opening and closing

#### `Toggle()`
Calls `Close()` if open, otherwise `Open()`.

#### `Open(JournalSection? section = null)`
If not already open, it:
1. Remembers the mouse mode, then makes the cursor visible (your FPS camera probably captures it).
2. If `PauseGame` is on, remembers the pause state, then pauses the game.
3. Shows the window and emits `Opened`.

Then it shows the contents of `section`, or of the last section if none is given.
```csharp
journalUI.Open(JournalSection.Items);   // open straight on Items
```

#### `Open(JournalEntry entry)`
Opens the journal straight on one entry's page. Useful for a "Read now" button after a pickup:
```csharp
pickup.PickedUp += entry => journalUI.Open(entry);
```

#### `Close()`
Hides the window, restores the pause state and mouse mode it saved in `Open`, and emits `Closed`.

### Functions — pages

#### `ShowContents(JournalSection section)`
Shows the first page of a section.
1. Sets `CurrentSection` and `CurrentEntry = null`.
2. Shows `ContentsPage`, hides `EntryPage`, sets the header text.
3. `RebuildContents()` creates the rows.
4. `RefreshBookmarks()` highlights the right bookmark.

Clicking a bookmark calls this, including the bookmark that's already active, which returns you to its contents.

#### `ShowEntry(JournalEntry entry)`
Shows one entry's page. It does nothing if the entry isn't owned.
1. Sets `CurrentSection` (to the entry's section) and `CurrentEntry`.
2. Hides contents, shows the entry page.
3. Fills in the title, picture (hidden if none) and text, and scrolls to the top.
4. `UpdatePageNavigation()` sets the page number and Prev/Next.
5. **`_journal.MarkRead(entry)`** removes the "!".
6. `RefreshBookmarks()`.

#### `Flip(int direction)`
`-1` goes to the previous entry, `+1` to the next, within the same section and in contents order. It does nothing at the ends or when the contents page is shown. Flipping onto an unread page marks it read too, because it goes through `ShowEntry`.

#### `UpdatePageNavigation()` (private)
Finds the current entry's position in the section, writes `"index / count"` into `PageNumber`, and disables `PrevButton` on the first page and `NextButton` on the last.

#### `RebuildContents()` (private)
Rebuilds the contents list from scratch:
1. Removes and frees every old row in `ContentsList`.
2. Gets `GetEntries(CurrentSection)`. If there are none, shows `ContentsEmptyLabel`.
3. Creates one row per entry with `CreateContentsLink`, numbered from 1.
4. Gives keyboard/gamepad focus to the first link (deferred, because new nodes can't take focus in the same frame).

Rebuilding everything is simpler than updating rows one by one, and a journal list is short, so the cost doesn't matter.

#### `CreateContentsLink(JournalEntry entry, int number, out Button button)` (private)
Builds one contents row in code:
```
HBoxContainer
├─ Label   "!" or ""      (16 px wide, so titles stay aligned whether or not there is a marker)
└─ Button  [icon] "N. Title"   (flat, fills the width, long titles end with "…")
```
- The button's `Pressed` signal calls `ShowEntry(entry)`.
- `ThemeTypeVariation = "JournalContentsLink"` lets you restyle every link from a Theme without code: in your Theme, add type `JournalContentsLink` with base type `Button`.
- `icon_max_width = 32` keeps big icons from blowing up the row.
- `out button` returns the button so `RebuildContents` can focus the first one.

#### `RefreshBookmarks()` (private)
For each bookmark:
- `SetPressedNoSignal(section == CurrentSection)` highlights the active one. "NoSignal" means changing it from code doesn't fire `Pressed` again, which would cause a loop.
- `SetUnread(HasUnread(section))` shows or hides its "!".

It runs on every `UnreadChanged` and every page change.

### Functions — reacting to the manager

#### `OnEntryAdded(JournalEntry entry)` (private)
If the journal is open on the same section, it rebuilds the contents list, or updates the page count if an entry page is open. Otherwise it does nothing, because the list is rebuilt anyway the next time it's shown.

#### `OnReloaded()` (private)
After `Load` or `Clear`: if open, it shows the contents again, since the old entry may no longer exist; if closed, it forgets the open entry.

#### `SectionTitle(JournalSection section)` (private)
Maps a section to its header text (`NotesTitle` / `ItemsTitle`).

### About `Tr(...)`
Texts go through `Tr()`, which is Godot's translation lookup. Without translation files it returns the text unchanged. With them, `Title` and `Description` can be translation keys.

---

## 7. `JournalBookmark` — section buttons

`JournalBookmark.cs` · `[GlobalClass] partial class JournalBookmark : Button`

A Button that knows which section it opens and has an "!" badge. `JournalUI` does all the clicking logic; the bookmark only **looks** right.

### Exports
| Export | Purpose |
|---|---|
| `Section` | Which section this bookmark opens. |
| `UnreadBadge` | The "!" node (usually a child Label). Shown while the section has unread entries. |
| `UseDefaultStyle` | On: the ribbon look is generated in code. Off: use your own Theme or StyleBoxTexture art. |
| `RibbonColor` | Ribbon colour (normal is darker, hover is this colour, active is lighter). |
| `TextColor` | Label colour on the ribbon. |
| `HoverOffset` | How many pixels the ribbon slides out on hover. |
| `ActiveOffset` | How far the **active** bookmark slides out. |

### Functions

#### `_Ready()`
Sets `ToggleMode = true`, so the button has a "pressed" state (the active section). Applies the default style if enabled. Hides the badge at the start.

#### `SetUnread(bool unread)`
Shows or hides `UnreadBadge`. Called by `JournalUI.RefreshBookmarks`.

#### `ApplyDefaultStyle()` (private)
Creates three ribbon styles (normal, hover, pressed) and applies them as **theme overrides** for the button states `normal`, `hover`, `pressed` and `hover_pressed`. `focus` gets an empty style so no focus outline is drawn. Sets the same text colour for all states.

#### `Ribbon(Color color, float extend)` (private, static)
Builds one `StyleBoxFlat`: right corners rounded, a soft shadow, inner padding. The trick is **`ExpandMarginRight = extend`**: the box is *drawn* wider than the button without changing its layout size, so the ribbon appears to slide out while the container doesn't shift.

### Using your own art
Turn off `UseDefaultStyle`, then in the Inspector under **Theme Overrides → Styles** set `normal`, `hover` and `pressed` to `StyleBoxTexture`s with your ribbon images. To make the active one stick out, give its `pressed` texture a bigger `Expand Margin Right`.

---

## 8. Common tasks

**Add a new note to the game**
1. FileSystem → New Resource → JournalEntry → `letter_02.tres`; fill in Title, Description, Section = Notes.
2. Put a `JournalPickup` on the object and set `Entry = letter_02.tres`.

**Give an entry without a pickup (quest, dialogue)**
```csharp
JournalManager.Instance.Add(GD.Load<JournalEntry>("res://Journal/map.tres"));
```

**Show "!" on the HUD while anything is unread**
```csharp
public override void _Ready()
{
    JournalManager.Instance.UnreadChanged += Refresh;
    Refresh();
}
public override void _ExitTree() => JournalManager.Instance.UnreadChanged -= Refresh;
private void Refresh() => Visible = JournalManager.Instance.HasAnyUnread;
```

**Show a "New entry" popup**
Subscribe to `JournalManager.Instance.EntryAdded` and show `entry.Title`.

**Check whether the player has read a note (quest condition)**
```csharp
var j = JournalManager.Instance;
bool readIt = j.Has(note) && !j.IsUnread(note);
```

**Don't pause the game while the journal is open**
Turn off `PauseGame` on `JournalUI`. You'll need to stop player input yourself (listen to `Opened` / `Closed`).

**Change the order in the contents list**
Give entries `SortOrder` values (10, 20, 30… leaves room to insert later).

---

## 9. Adding a third section (example: Map)

1. **`JournalEntry.cs`:** add `Map` to `enum JournalSection`. Add it at the **end**, because enums are saved as numbers in `.tres` and `.tscn` files, and inserting in the middle would shift existing values.
2. **`JournalUI.cs`:** add `[Export] public string MapTitle = "Карты";` and a `JournalSection.Map => MapTitle` arm in `SectionTitle`.
3. **Scene:** duplicate a bookmark in `Bookmarks`, set its `Section = Map`, text and colour. No code change is needed for the bookmark, since `JournalUI` finds all bookmarks in the container.

---

## 10. Troubleshooting

| Problem | Likely cause |
|---|---|
| J does nothing | `ToggleAction` doesn't match the Input Map name exactly, or the journal scene isn't in the level. |
| Error "autoload JournalManager не найден" | Autoload missing in Project Settings → Globals. |
| Buttons don't respond while open | `JournalUI` isn't `ProcessMode = Always` (set in `_Ready`); check that no parent overrides it with `Disabled`. Also check that `Dim` or another full-screen node with Mouse Filter = Stop isn't **on top** of the book (it must come earlier in the tree). |
| Text shows `[b]` literally | `EntryText` → turn on **BBCode Enabled**. |
| Picked-up letter reappears after loading | Entry isn't a `.tres` file, or the save was loaded after the level was instanced. |
| The same entry can't be added twice | That's intended: `Add` returns `false` for owned entries. Two entries need two different keys (`Id` or files). |
| Bookmark "!" never shows | `UnreadBadge` export not assigned on the bookmark. |
| Bookmarks don't switch sections | Bookmarks are not **direct** children of `BookmarkContainer`, or `Section` is the same on both. |
