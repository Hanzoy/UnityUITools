# Changelog

## 0.1.3

- Required Agent generation requests to select an explicit `settingsDataName`, independent of the UI window's last-selected Panel/Popup mode.
- Added safe Agent cleanup guidance for transient request files and one-off JSON intermediates.
- Preserved the selected setting's auto-open behavior across the post-compilation auto-bind step.

## 0.1.2

- Added Agent-oriented UI tree, binding spec, and binding generation request workflows.
- Added `*.uibind.json` import/export conversion for `UIPanelBindings` assets.
- Unified request files under `Assets/UIBindRequests`.
- Improved binding window refresh when UI prefab hierarchy or assets change.

## 0.1.0

- Initial package structure
- Migrated `UIBindTool` editor demo into the package
- Added `UIPanelTemplate.txt` template file
