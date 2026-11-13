# Tool decoupling plan

Status: complete
Started: 2026-11-13
Owner workspace: current working tree (which already contains unrelated, uncommitted changes)

## Goal

Make tools follow player intent: reads only read, one action does one named job, batch scope is
explicit, and no routine action hides an unrelated decision. Reduce repeated replanning, accidental
mode selection, surprise writes and menu-driven play without breaking existing MCP or HTTP clients.

## Layout and compatibility rules

- New semantic implementations live in `mod/StardewMCP/Agent/Semantic/`.
- Thin `ToolDef` registration/forwarding shims stay in top-level `Agent/*.cs`, because external
  tooling reads definitions there (`CONTRIBUTING.md`).
- Existing tool names and HTTP routes remain callable. A replaced tool is marked `Legacy`; it is not
  deleted or renamed.
- `AgentTools.All()` remains the compatibility catalog used by MCP and statistics.
- `AgentTools.Preferred()` is the built-in AI catalog: legacy tools with semantic replacements are
  hidden there. `/api/contract` exposes `legacy` so external clients can migrate deliberately.
- New actions take a required `reason`; new pure observations are `Instant` and need no `action`
  parameter. Generic `menu` and `interact` remain fallbacks until semantic coverage is complete.
- Each landed phase updates this file, `docs/tools.md`, `docs/tools.th.md`, API/feature docs when
  applicable, and the source/schema tests in the same change.

## Phase 0 — Baseline and invariants

- [x] Inventory every MCP tool, internal HTTP operation, debug operation and control entry point.
- [x] Verify all existing tool groups are registered and identify mixed read/write tools.
- [x] Record the pre-existing dirty worktree; do not revert or overwrite unrelated work.
- [x] Establish the additive compatibility strategy and separate semantic source folder.

Acceptance: the audit distinguishes 75 registered MCP tools (74 when `guide` is disabled), two
HTTP-internal execution units and 38 debug operations. No old name is removed by the refactor.

## Phase 1 — Compatibility mechanism and pilot split

Status: landed in source; representative verification completed in Phase 6

### Catalog mechanism

- [x] Add `ToolDef.Legacy`, `AgentTools.Preferred()` and contract metadata.
- [x] Make the built-in AI use `Preferred()` while MCP keeps `All()`.
- [x] Keep `accept_quest`, `bundles`, `donate_bundle` and unrelated tools unchanged.

### Quest tools

- [x] Keep `quests` as legacy compatibility entry.
- [x] Add `list_quests()` as a pure instant observation.
- [x] Add `claim_quest_reward(quest?, reason)` as the only reward-claim action.

### Museum tools

- [x] Keep `museum` as legacy compatibility entry.
- [x] Add `list_museum_donations()` as a pure instant observation.
- [x] Add `donate_museum(item, reason)`; `item=all` remains explicit.
- [x] Add `claim_museum_rewards(reason)`.

### Special Order tools

- [x] Keep `special_orders` as legacy compatibility entry.
- [x] Add `list_special_orders(reason)` for the walk-to-board query.
- [x] Add `active_special_orders()` as a pure instant observation.
- [x] Add `accept_special_order(index, reason)`.

Acceptance: the three legacy tools still execute their old modes; the eight new tools expose no
mode-switch `action`; the built-in AI receives only the replacements; all schema/docs tests pass.
With `guide` enabled, the compatibility catalog has 83 tools and the preferred catalog has 80;
without it they have 82 and 79 respectively. The source build and both game-free suites passed.
Real in-game calls are not claimed yet.

## Phase 2 — Production, animals and buildings

Status: landed in source; representative verification completed in Phase 6

- [x] Split `craft` into `list_recipes` and `craft_item`; `cook` into `list_dishes` and `cook_dish`.
- [x] Split `crack_geode` into `list_geodes` and `crack_geodes`.
- [x] Split `buy_animal` into `list_animals_for_sale` and `purchase_animal`.
- [x] Split `animals` into `list_animals`, `pet_animals`, `collect_animal_products` and `feed_animals`.
- [x] Split `build` into `list_buildings`, `suggest_building_site`, `construct_building` and
  `upgrade_building`; the new upgrade action has a static action lane and does not depend on cached names.
- [x] Preserve `upgrade_house`; its hard-coded prices were not changed in this phase.
- [x] Keep the six mixed-mode predecessors callable and mark them Legacy.

Acceptance: the 16 new tools have no mode-switch `action`; empty targets cannot select a write;
all seven observations are statically Instant; writes have explicit targets and required reasons;
old tools remain callable. With `guide` enabled, the compatibility catalog has 99 tools and the
preferred catalog has 90; without it they have 98 and 89. Real in-game calls are not claimed yet.

## Phase 3 — Containers and generic menus

Status: landed in source; representative verification completed in Phase 6

- [x] Split `chest` into `list_chest`, `list_containers`, `store_items` and `take_items`, with
  `open_container` as the explicit walk-and-open action.
- [x] Make current/open-container and whole-farm reads Instant; keep transfers serialized.
- [x] Add `read_menu`, `choose_mine_floor`, `choose_menu_option`, `set_menu_amount` and `confirm_menu`.
  Existing `name` remains the naming tool; `menu` remains the preferred unknown-menu fallback.
- [x] Add `open_container`, `load_machine`, `use_held_item` and `read_sign` with target/effect checks;
  `interact` remains the preferred fallback for other tile actions.
- [x] Split `talk_to` into `talk_to_villager` (no gift path) and `give_gift` (required gift), sharing
  the same NPC approach and game-native interaction implementation.
- [x] Return remaining/skipped transfers and reject unsupported targets instead of reporting a generic
  right-click as success.

Acceptance: routine container, elevator, amount, confirmation, machine, sign and gifting workflows no
longer require a mode-switch `action`; unknown menus remain escapable; container reads do not unfreeze
time. `read_sign` is correctly an action because it walks. The 15 new tools bring the compatibility
catalog to 114 and the preferred catalog to 103 with `guide` enabled (113/102 without it). Real
in-game calls are not claimed yet.

## Phase 4 — Dangerous defaults and hidden side effects

Status: landed in source; representative verification completed in Phase 6

- [x] `plant_item` requires exactly one of `tiles`, `area`, or `all_empty=true`; the whole-map scope
  also requires `confirm=true`. Legacy `plant` retains its old implicit scope.
- [x] `dismiss_menu` never accepts/claims; `accept_letter_quest` and `claim_menu_items` make those
  decisions explicit. Closing a letter/item menu that would force a hidden decision is refused safely.
- [x] `sleep` has no journal gate, uses `confirm_early`, and never claims through a menu on the way to
  bed. Legacy `go_to_bed` keeps its journal and old menu behavior. All sleep-sensitive runtime rules
  recognize both names through `ToolCatalog.IsSleep`.
- [x] `travel_to_location` accepts deterministic places only; `travel_to_shop` handles shops/keepers
  and stops at the counter. Social travel uses `talk_to_villager`/`give_gift`; sightings cover them.
- [x] `equip` now covers hats, shirts, pants, rings, boots and rod attachments; `wear` is Legacy.
- [x] `rest` documents its multiplayer-only restriction; `fight.seconds` is optional with default 10.

Acceptance: the preferred catalog cannot plant a large area from an omitted scope; routine closing
makes no quest/item decision; sleep is independent of AI bookkeeping; travel and equipment intents
are explicit. Phase 4 added seven tools (four in 4A, three in 4B), producing 121 compatibility and
105 preferred tools with `guide` enabled (120/104 without it). Real in-game calls are not claimed yet.

## Phase 5 — Execution lanes and result contract

Status: landed in source; representative verification completed in Phase 6

- [x] `zeroTime`, `readOnly` and `serialized` are separate contract fields; v1 `instant`/`instantFor`
  remain available. `ApiVersion` is 2.
- [x] `pay`, `give` and `name` are zero-time serialized mutations; `think`/`say`/`emote`/`journal`
  remain responsive meta operations. Debug operations have no misleading Instant flag.
- [x] Pure observations remain responsive and omit the duplicate footer; other action/meta footer
  behavior remains compatible.
- [x] Results support `changed`, explicit `no_change`, partial `data.stoppedBecause`, batch
  done/remaining/skipped and `invalid_args` with `data.validValues` (adopted by the action enums).
- [x] Explicit no-change results do not count as loop progress. Common idempotent menu/horse/quest/
  animal paths use `NoChange`; ordinary legacy results retain unknown `changed` when unclassified.
- [x] MCP `tools/call` validates missing/wrong `params`, `name` and `arguments` instead of throwing
  an HTTP 500.

Acceptance: zero-time mutations serialize without unfreezing; observations do not masquerade as
writes or vice versa; equivalent semantic/legacy paths share outcomes; idempotent retries are
machine-distinguishable. `Reasons` and `CallContext` remain in `ToolResult.cs` for external readers,
with pure reply rules in `OutcomeRules.cs` and daily summary code in `DaySummary.cs`.

## Phase 6 — Decision cadence, migration and verification

Status: complete (source, migration and isolated representative in-game verification)

- [x] Update the built-in prompt: choose 1–3 daily goals, prepare before travel, finish one goal before
  switching, re-observe after failure/interruption rather than after every successful action.
- [x] Prefer semantic tools in prompts and guide text; retain generic menu/interact only as fallbacks.
- [x] Update English and Thai tool docs, feature/coverage notes and API contract examples.
- [x] Run `python -m pytest -q mod/tests` and `dotnet test mod/StardewMCP.Tests` after every phase.
- [x] Build the mod with deploy/zip disabled where the local game installation permits it.
- [x] Exercise the changed tools in a copied smoke-test save through real HTTP calls; verify both the
  new preferred calls and old compatibility calls. Record any game-dependent checks not run.

The isolated verifier uses a separate APPDATA, Mods directory and port 8772 with `Brain=none`; the
original game on port 8770 is not driven by the verifier. Migration guidance is in
`docs/tool-decoupling/migration.md` and result/execution terms are in `CONTEXT.md`.

Final acceptance: unit/schema suites pass, the mod builds, new and legacy calls work in game, the
built-in AI receives no legacy pilot tools, and this ledger identifies any remaining phase exactly.

## Landing log

### 2026-11-13 — Phase 0

Completed the full operation audit and compatibility design. No game code was changed by the audit.

### 2026-11-13 — Phase 1

Landed the catalog mechanism and the quests/museum/special-orders pilot in
`Agent/SemanticTools.cs` and `Agent/Semantic/`. Added eight semantic tools, retained the three legacy
entry points, exposed `legacy` through `/api/contract`, and switched the built-in AI to `Preferred()`.
Updated English and Thai tool/feature documentation and coverage notes.

Verification on the integrated worktree:

- `python -m pytest -q mod/tests`: 101 passed, 1 pre-existing xpass for the old close_menu marker.
- `dotnet test mod/StardewMCP.Tests --nologo`: 821 passed.
- `dotnet build mod/StardewMCP/StardewMCP.csproj -p:EnableModDeploy=false -p:EnableModZip=false --nologo`:
  succeeded with 0 warnings and 0 errors.
- `git diff --check`: passed.
- In-game old/new HTTP calls: not run; the running game was not redeployed or interrupted. This remains
  an explicit Phase 6 verification item, not a claimed success.

Next implementation phase: Phase 2 production, animals and buildings. Phases 4 and 5 in the task
tracker remain separate, later work; no dangerous default or execution-lane behavior was changed here.

### 2026-11-13 — Phase 2

Landed 16 semantic replacements for crafting, cooking, geodes, animal purchase/care and buildings.
The six mixed-mode predecessors remain available as Legacy tools. Building construction and upgrade
now have separate static action paths; site suggestion and catalogues are pure observations.

Integration review removed a duplicate, unregistered cooking registry, added the missing optional
`name` schema for animal-product collection, corrected stale implementation comments, and added tests
that prohibit hidden registries under `Agent/Semantic/` and mode-switch arguments in semantic tools.

Verification on the integrated worktree:

- `python -m pytest -q mod/tests`: 103 passed, 1 pre-existing xpass.
- `dotnet test mod/StardewMCP.Tests --nologo`: 827 passed.
- `dotnet build mod/StardewMCP/StardewMCP.csproj -p:EnableModDeploy=false -p:EnableModZip=false --nologo`:
  succeeded with 0 warnings and 0 errors.
- `git diff --check`: passed.
- In-game old/new HTTP calls: not run; no running game was redeployed or interrupted. Phase 6 still
  owns real gameplay verification.

Next implementation phase: Phase 3 containers and generic menus. Dangerous defaults and execution
lanes remain separate later phases and were not changed here.

### 2026-11-13 — Phase 3

Landed 15 semantic tools for containers, frequent menus, one-tile interactions and villager social
actions. `chest` and `talk_to` remain callable as Legacy; `menu` and `interact` remain available to
the built-in AI only as generic fallbacks. Container and machine actions validate their targets and
outcomes rather than treating any right-click as success.

Integration review corrected the English/Thai `read_sign` timing documentation, exposed the optional
`list_chest` filters in its schema, aligned container hints with `open_container`, and clarified the
machine/use-item descriptions.

Verification on the integrated worktree:

- `python -m pytest -q mod/tests`: 103 passed, 1 pre-existing xpass.
- `dotnet test mod/StardewMCP.Tests --nologo`: 830 passed.
- `dotnet build mod/StardewMCP/StardewMCP.csproj -p:EnableModDeploy=false -p:EnableModZip=false --nologo`:
  succeeded with 0 warnings and 0 errors.
- `git diff --check`: passed.
- In-game old/new HTTP calls: not run; no running game was redeployed or interrupted. Phase 6 still
  owns real gameplay verification.

Next implementation phase: Phase 4 dangerous defaults and hidden side effects. Execution-lane and
result-contract changes remain Phase 5 and were not changed here.

### 2026-11-13 — Phase 4

Landed Phase 4A explicit planting scope and menu decisions, then Phase 4B sleep/travel/equipment
semantics. The old tools remain compatible as Legacy. Runtime name-based sleep handling was centralized
with `IsSleep`; travel and social sightings were updated, including the Phase 3 semantic social tools.

Verification after Phase 4A: 103 Python tests passed, 832 C# tests passed, build 0 warnings/errors.
Verification after Phase 4B: 107 Python tests passed (1 pre-existing xpass), 841 C# tests passed,
build 0 warnings/errors, and `git diff --check` passed. Integration review also aligned the planting
`all_empty` + `confirm` documentation and letter-attachment tool descriptions.

Real old/new in-game calls remain pending for Phase 6; no running game was redeployed or interrupted.

Next implementation phase: Phase 5 execution lanes and result contract.

### 2026-11-13 — Phase 5

Separated zero-time, read-only and serialized execution. `pay`/`give`/`name` now serialize without
unfreezing, pure observations skip the duplicate footer, and `no_change` is visible to the loop guard.
Added partial stop causes, valid values, batch change inference and MCP argument validation. API v2
retains v1 fields. Restored the required `Reasons`/`CallContext` layout in `ToolResult.cs` after review;
daily summaries live separately in `DaySummary.cs`.

Final Phase 5 suites: 107 Python tests passed (1 pre-existing xpass), 865 C# tests passed before the
Phase 6 prompt tests, and the mod built with 0 warnings/errors. `git diff --check` passed.

### 2026-11-13 — Phase 6 and final verification

Updated the built-in daily cadence and all strategy-guide examples to the preferred semantic tools.
Added `migration.md` with all 16 Legacy mappings and documented the v2 execution/result terms in
`CONTEXT.md`. The catalog has 121 compatibility and 105 preferred tools with `guide` enabled (120/104
without it), including 46 semantic tools.

Final game-free verification:

- `python -m pytest -q mod/tests`: 112 passed, 1 pre-existing xpass.
- `dotnet test mod/StardewMCP.Tests --nologo`: 868 passed.
- `dotnet build mod/StardewMCP/StardewMCP.csproj -p:EnableModDeploy=false -p:EnableModZip=false --nologo`:
  succeeded with 0 warnings and 0 errors.
- `git diff --check`: passed.

Real HTTP verification used an isolated APPDATA, Mods directory, copied `smoke_semantic` save and port
8772 with `Brain=none`. It never drove or deployed to the original save on port 8770. The first run
passed 19/21 checks; the two failures were test-placement issues (a blocked sign and no continuous
four-tile strip), not tool-contract failures. Focused supplementary calls passed both, giving 21/21
completed checks: API v2 and 121 tools, 16 Legacy names, preferred entries and lane flags, old/new
quest and museum reads, valid values, no-change results, container/menu flows, sign reading, machine
loading, serialized naming, deterministic travel, explicit planting and partial stop reasons, legacy
planting, and `sleep(confirm_early=true)` crossing a day without a journal. A separate direct MCP call
also confirmed missing/wrong `params.name` returns `invalid_args` with HTTP 200, not a 500.

Evidence is preserved in the workspace `.testartifacts/results/verify-result.json` and
`verify-supplement-result.json`. The isolated game was stopped, its launch directory was removed, and
the accidental duplicate test folder inside the repository was removed. The original game was not
hot-reloaded during that verification.

Not claimed as verified: real multiplayer `pay`/`give` transfers, high-cost construction/purchases,
romance, festivals and late-game travel, or every one of the 121 tools' individual gameplay paths.
Those require suitable multiplayer/late-game fixtures and are not marked as failures. All planned
refactor phases are landed and their representative new/old compatibility paths have been exercised.
