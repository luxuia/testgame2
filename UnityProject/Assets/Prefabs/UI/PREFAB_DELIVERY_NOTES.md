# UI Prefab Delivery Notes

## Naming Rules

- Core root prefab names are fixed:
  - `UIRoot.prefab`
  - `HUDRoot.prefab`
  - `SidePanel.prefab`
  - `ModalTemplate.prefab`
  - `CombatOverlay.prefab`
- Runtime clone names are expected to be Unity default `<PrefabName>(Clone)`.
- Layer node names under `UIRoot` are fixed:
  - `HUDLayer`
  - `SidePanelLayer`
  - `CombatOverlayLayer`
  - `ModalLayer`

## Required Serialized/Structural Checklist

- `UIRoot` must include:
  - `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `UIManager`, `UIPrefabViewLoader`, `UIRuntimeRegistry`, `UICommandRouter`, `UILayerPolicy`
  - `UIManagerBindingBridge` for fallback binding
  - child layers: `HUDLayer`, `SidePanelLayer`, `CombatOverlayLayer`, `ModalLayer`
- `HUDRoot` must include slots:
  - `StatusSlot`, `HotbarSlot`, `ManaCapacitySlot`, `FixedButtonsSlot`
- `SidePanel` must include:
  - `PanelBody`
  - tab slots: `Tab_Build`, `Tab_Slime`, `Tab_Adjutant`, `Tab_Resource`, `Tab_Quest`, `Tab_Diplomacy`
- `ModalTemplate` must include:
  - `TitleSlot`, `ContentRoot`, `PaginationSlot`, `CloseButton`
- `CombatOverlay` must include:
  - `BattleInfoRoot`, `BossWarningAnchor`

## Produced Prefab Assets

- `Assets/Prefabs/UI/UIRoot.prefab`
- `Assets/Prefabs/UI/HUDRoot.prefab`
- `Assets/Prefabs/UI/SidePanel.prefab`
- `Assets/Prefabs/UI/ModalTemplate.prefab`
- `Assets/Prefabs/UI/CombatOverlay.prefab`

## Runtime Loading Strategy

- Runtime uses single-source prefab references from `Assets/Prefabs/UI/`.
- `UIBootstrap` references `UIRoot.prefab` directly.
- `UIPrefabViewLoader` references `HUDRoot/SidePanel/ModalTemplate/CombatOverlay` directly.
- No duplicate `Resources/UI` copy is maintained.

## Placeholder Coverage

- All five core prefabs are skeleton-complete and can be instantiated in play mode.
- `UIBootstrap` in `SinglePlayer` creates exactly one runtime `UIRoot`.
- Default startup opens `HUDRoot`, `SidePanel`, and `CombatOverlay` via `UIManager` lifecycle.
- `ModalTemplate` is registered and routed but not auto-opened on startup.

## Known Gaps For Next Iteration

- Final visual polish (pixel art skin, spacing, typography) is not implemented.
- Real gameplay data binding (status/mana/resources/alerts) is still placeholder level.
- Modal content pages (sin skill tree, slime detail, adjutant detail) are not yet mounted.
- Input hints and dedicated gamepad UX adaptation remain pending.
