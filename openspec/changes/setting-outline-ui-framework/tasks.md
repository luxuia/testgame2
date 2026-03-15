## 1. Framework Skeleton

- [ ] 1.1 Create `UIRoot` entry with fixed layer containers: `HUDLayer`, `SidePanelLayer`, `ModalLayer`, `CombatOverlayLayer`
- [ ] 1.2 Register layer order and input priority policy so modal blocks lower interactive layers by default
- [ ] 1.3 Add scene bootstrap wiring in `SinglePlayer` to ensure UI root initializes once per runtime

## 2. Unified Input Routing

- [ ] 2.1 Implement a UI command router that maps mouse/keyboard/gamepad input into unified commands
- [ ] 2.2 Add context resolution rules for camera-control vs unit-control states without per-page direct input listeners
- [ ] 2.3 Add conflict guards so one input event resolves to exactly one command path in a frame

## 3. Core Views (Stage-1 Skeleton)

- [ ] 3.1 Create `HUDRootView` skeleton with four persistent module placeholders (status, hotbar, mana-capacity, fixed buttons)
- [ ] 3.2 Create collapsible `SidePanelView` skeleton with six tabs (build, slime, adjutant, resource, quest, diplomacy)
- [ ] 3.3 Create shared `ModalTemplateView` shell and register secondary pages to use it
- [ ] 3.4 Create `CombatOverlayView` skeleton with event-driven show/hide behavior

## 4. Aggregated State Bridge (Stage-2 Functional Binding)

- [ ] 4.1 Define aggregated UI state contracts for HUD, side panel, and combat overlay
- [ ] 4.2 Implement domain adapters to feed aggregated state from fungal carpet, combat, and management systems
- [ ] 4.3 Bind live state to HUD core fields and invasion alert replacement behavior
- [ ] 4.4 Bind side panel unlock visibility rules for guided mode hidden tabs

## 5. Validation and Hardening

- [ ] 5.1 Verify modal focus/close behavior and lower-layer interaction blocking policy
- [ ] 5.2 Verify combat lifecycle events correctly toggle combat overlay visibility
- [ ] 5.3 Run compile validation: `dotnet build UnityProject/Assembly-CSharp.csproj -nologo`
- [ ] 5.4 Perform scene runtime check in `Assets/Scenes/SinglePlayer.unity` for HUD, panel toggle, and command routing

## 6. Documentation Sync

- [ ] 6.1 Update root milestone docs (`Plan.md`/`Implement.md`/`Documentation.md`) with UI framework scope and stage gates
- [ ] 6.2 Record known limitations and follow-up tasks for secondary panel content expansion
