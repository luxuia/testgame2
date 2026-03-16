## 1. Prefab Directory and Naming Baseline

- [x] 1.1 Create or standardize UI prefab directory layout under `UnityProject/Assets/Prefabs/UI/`
- [x] 1.2 Define naming rules for core prefabs (`UIRoot`, `HUDRoot`, `SidePanel`, `ModalTemplate`, `CombatOverlay`)
- [x] 1.3 Add checklist for required serialized references per core prefab

## 2. UIRoot Assembly

- [x] 2.1 Create `UIRoot.prefab` with fixed child layers for `HUD`, `SidePanel`, `Modal`, and `CombatOverlay`
- [x] 2.2 Configure layer ordering and raycast/input priority policy
- [x] 2.3 Validate `UIRoot` can be instantiated without missing component errors

## 3. Core View Prefab Production

- [x] 3.1 Produce `HUDRoot.prefab` shell with status/hotbar/mana-fixed-button module slots
- [x] 3.2 Produce `SidePanel.prefab` shell with collapsible container and six tab slots
- [x] 3.3 Produce `ModalTemplate.prefab` shell with title/content/close/pagination structural slots
- [x] 3.4 Produce `CombatOverlay.prefab` shell with battle info placeholders and hide/show anchors

## 4. Runtime Wiring Compatibility

- [x] 4.1 Register core view prefabs for UI manager lifecycle open/close flows
- [x] 4.2 Verify reopen behavior does not create duplicate root objects
- [x] 4.3 Add or update bootstrap reference so `SinglePlayer` can load exactly one `UIRoot`

## 5. Validation and Acceptance

- [x] 5.1 Run editor-side missing reference checks for all core prefabs
- [x] 5.2 Run play-mode smoke test for open/close transitions of core views
- [x] 5.3 Verify side panel collapse/expand and tab scaffold availability
- [x] 5.4 Verify modal template close behavior is consistent across mounted pages

## 6. Delivery Sync

- [x] 6.1 Document produced prefab list, paths, and current placeholder coverage
- [x] 6.2 Record known gaps for next iteration (data binding detail pages and visual polish)
