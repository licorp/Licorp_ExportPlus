# Architecture Conventions (Phase 1 Foundation)

## Purpose
This document defines coding/architecture conventions for incremental refactor of LicorpExportPlus while preserving compatibility across Revit 2020–2027.

## 1. Layer Boundaries

### Bootstrap Layer
- Files: `ExportPlusApplication.cs`, `ExportPlusCommand.cs`
- Responsibilities:
  - Revit entry points (`IExternalApplication`, `IExternalCommand`)
  - DI bootstrap / composition root handoff
  - UI shell launch delegation
- Must **not** contain business export logic.

### Application/Service Layer
- Folder: `Source/LicorpExportPlus.Shared/Services/*`
- Responsibilities:
  - Export orchestration, profile processing, report generation
  - Stateless workflows where possible
  - External dependency integration behind interface boundaries
- Must avoid direct WPF control manipulation.

### Presentation Layer
- Folder: `Source/LicorpExportPlus.Shared/Views/*`
- Responsibilities:
  - UI state display
  - Input/output mapping between view model/state and service calls
- Keep code-behind thin; move heavy logic to services/use-cases.

### Utility Layer
- Folder: `Source/LicorpExportPlus.Shared/Utils/*`
- Responsibilities:
  - Pure helper logic without UI coupling
  - Reusable primitives for formatting/parsing/path helpers

## 2. Dependency Injection Rules
- Register dependencies centrally in startup composition root.
- Resolve runtime services via interfaces, not concrete classes where feasible.
- Disallow service construction via `new` in command/UI except framework-required objects.
- Prefer constructor injection for testability.

## 3. Revit Command/Event Rules
- Command class should validate context and delegate execution.
- ExternalEvent handlers should have single responsibility per workflow.
- Keep Revit API document operations wrapped in well-scoped service methods.

## 4. Error Handling Rules
- Never swallow exceptions silently.
- Catch at boundary points (command/startup/IO/event boundaries).
- Include actionable context in logs (operation name, target file/profile/sheet set).
- Preserve user-safe message for UI feedback; avoid raw stack traces in end-user dialogs.

## 5. Build/Script Rules
- Keep runtime-year mapping centralized and consistent.
- Exclude transient folders (`publish`, `obj`) from copy/package loops.
- Add verification checks after bundle/package generation.
- Any script change must be validated with `buildtest.bat Release All`.

## 6. Incremental Refactor Rules
- Small batches only; no big-bang refactor.
- Maintain behavior compatibility unless explicit test-backed change request.
- Each batch ends with build verification evidence.
- No commit/push until manual user validation is approved.
