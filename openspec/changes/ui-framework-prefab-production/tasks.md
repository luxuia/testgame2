## 1. Prefab Directory and Naming Baseline

- [ ] 1.1 Create or standardize UI prefab directory layout under `UnityProject/Assets/Prefabs/UI/`
- [ ] 1.2 Define naming rules for core prefabs (`UIRoot`, `HUDRoot`, `SidePanel`, `ModalTemplate`, `CombatOverlay`)
- [ ] 1.3 Add checklist for required serialized references per core prefab

## 2. UIRoot Assembly

- [ ] 2.1 Create `UIRoot.prefab` with fixed child layers for `HUD`, `SidePanel`, `Modal`, and `CombatOverlay`
- [ ] 2.2 Configure layer ordering and raycast/input priority policy
- [ ] 2.3 Validate `UIRoot` can be instantiated without missing component errors

## 3. Core View Prefab Production

- [ ] 3.1 Produce `HUDRoot.prefab` shell with status/hotbar/mana-fixed-button module slots
- [ ] 3.2 Produce `SidePanel.prefab` shell with collapsible container and six tab slots
- [ ] 3.3 Produce `ModalTemplate.prefab` shell with title/content/close/pagination structural slots
- [ ] 3.4 Produce `CombatOverlay.prefab` shell with battle info placeholders and hide/show anchors

## 4. Runtime Wiring Compatibility

- [ ] 4.1 Register core view prefabs for UI manager lifecycle open/close flows
- [ ] 4.2 Verify reopen behavior does not create duplicate root objects
- [ ] 4.3 Add or update bootstrap reference so `SinglePlayer` can load exactly one `UIRoot`

## 5. Validation and Acceptance

- [ ] 5.1 Run editor-side missing reference checks for all core prefabs
- [ ] 5.2 Run play-mode smoke test for open/close transitions of core views
- [ ] 5.3 Verify side panel collapse/expand and tab scaffold availability
- [ ] 5.4 Verify modal template close behavior is consistent across mounted pages

## 6. Delivery Sync

- [ ] 6.1 Document produced prefab list, paths, and current placeholder coverage
- [ ] 6.2 Record known gaps for next iteration (data binding detail pages and visual polish)
