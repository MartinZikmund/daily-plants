# Uno Platform issues and behaviour notes

Running log of Uno Platform bugs, API gaps and WinUI behaviour mismatches hit while
building Daily Plants — including hypotheses that turned out to be wrong, so the same
wrong idea does not get chased twice.

| Field | Meaning |
|---|---|
| **Uno.Sdk** | Version in `global.json` when the issue was seen |
| **Heads** | Which target frameworks are affected |
| **Impact** | What breaks for a user or a developer |
| **Workaround** | What the codebase does about it today |

---

## `WindowActivatedEventArgs.WindowActivationState` has a different type than WinUI

- **Uno.Sdk:** 6.6.0-dev.196
- **Heads:** `net10.0-windows10.0.26100` vs `net10.0-desktop` — the two disagree
- **Status:** Open, worked around
- **Impact:** Developer-facing, and there is **no single expression that compiles on both
  heads**. The property's type differs:

  | Head | Type of `args.WindowActivationState` |
  |---|---|
  | `net10.0-windows10.0.26100` (WinAppSDK) | `Microsoft.UI.Xaml.WindowActivationState` |
  | `net10.0-desktop` (Uno/Skia) | `Windows.UI.Core.CoreWindowActivationState` |

  Writing the comparison for either head breaks the other with
  `CS0019: Operator '==' cannot be applied to operands of type 'WindowActivationState' and
  'CoreWindowActivationState'` (and the reverse). On the Windows head this also cascades
  into `MSB3073` from `XamlCompiler.exe`, which obscures the real cause.
- **Workaround:** Do not read the property. `DiaryView.Window_Activated` handles every
  activation, including deactivation, because the work it does is idempotent. If a future
  handler genuinely needs the state, it will need a `#if WINDOWS` block — the codebase
  otherwise has zero platform-conditional C#, so that is worth avoiding.
- **Caught by:** building `net10.0-windows10.0.26100`. The desktop head alone compiled
  fine, so **always build both heads before trusting a view-layer change.**
- **Not yet filed** on unoplatform/uno.

---

## `dotnet format` (non-whitespace) breaks the build on this project

- **Uno.Sdk:** 6.6.0-dev.196 · **.NET SDK:** 10
- **Heads:** N/A — tooling
- **Status:** Open, worked around
- **Impact:** A plain `dotnet format <project>` applies analyzer code fixes as well as
  formatting. On `Services/ExportService.cs` it "fixed" the IL2026 trimming warnings from
  `JsonSerializer` by inserting a bare `[RequiresUnreferencedCode()]` on the enclosing
  methods. That attribute has a required `message` parameter, so the result does not
  compile: `CS7036: There is no argument given that corresponds to the required parameter
  'message'`.
- **Workaround:** Use `dotnet format whitespace` for routine formatting on this repo.
  Not confirmed to be Uno-specific — it may reproduce on any project with trim analyzers
  enabled — but it reproduces reliably here.
- **Not yet filed.**

---

## Unverified: WebAssembly SQLite path may not be persisted

- **Uno.Sdk:** 6.6.0-dev.196
- **Heads:** `net10.0-browserwasm`
- **Status:** **Unverified hypothesis — do not act on it until checked**
- **Hypothesis:** `SqliteDataService.GetDefaultDatabasePath()` resolves the database via
  `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` rather than
  `ApplicationData.Current.LocalFolder`. Uno's file-management documentation states that
  only `LocalFolder` / `RoamingFolder` / `SharedLocalFolder` are backed by IndexedDB on
  WebAssembly; everything else lives in in-memory MEMFS and is lost on refresh. The
  connection is also opened synchronously in the constructor, before any await that would
  let IDBFS finish mounting.
- **How to settle it:** deploy the WASM head, log the resolved path, log some servings,
  hard-refresh, and check the data survives. Record the answer here either way.
- **If confirmed:** switch to `ApplicationData.Current.LocalFolder.Path` and move
  connection creation out of the constructor into an awaited `InitializeAsync`.
