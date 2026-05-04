# LicorpExportPlus Migration Baseline (Phase 0)

> Scope: establish **as-is baseline** and **to-be direction** before deep refactor.
> Constraint: keep Revit compatibility (2020–2027), avoid business behavior changes without test cover.

## 1) Baseline Snapshot (Current State)

### 1.1 Build/Runtime Matrix
- Runtime projects:
  - `Source/LicorpExportPlus.R20` → Revit 2020–2024
  - `Source/LicorpExportPlus.R25` → Revit 2025–2026
  - `Source/LicorpExportPlus.R27` → Revit 2027
- `buildtest.bat` supports `R20 | R25 | R27 | All`, builds and copies outputs to `Build Test/*/Release`.
- Current status (latest run): build test all targets is green.

### 1.2 Bootstrap / Application Startup (As-Is)
File: `Source/LicorpExportPlus.Shared/ExportPlusApplication.cs`

Observed pattern:
- Good:
  - Dedicated `IExternalApplication` bootstrap.
  - Container exists (`ricaun.DI` + `ricaun.Revit.DI`).
  - Ribbon creation encapsulated.
  - Startup trace logging present (`LicorpTrace`).
- Gaps:
  - Composition root is still minimal (`CreateContainer` currently only `AddRevitSingleton`).
  - Service registration strategy is not centralized for all app services.
  - Lifecycle/disposal is only partial (`RevitTaskService` disposed, no broader host-style lifecycle).

### 1.3 Command Flow (As-Is)
File: `Source/LicorpExportPlus.Shared/ExportPlusCommand.cs`

Observed pattern:
- Good:
  - Command-level guard for `ActiveUIDocument`.
  - Error handling and user-facing message flow.
- Gaps:
  - Window created directly in command (`new ExportPlusMainWindow(doc, uiApp)`), causing UI coupling.
  - Static `_window` lifecycle in command class; difficult to test and scale.
  - No DI-based factory/launcher abstraction for UI shell.

### 1.4 Logging (As-Is)
File: `Source/LicorpExportPlus.Shared/Logger.cs`

Observed pattern:
- Good:
  - Serilog configuration exists, rolling file, retention, app property enrichment.
- Gaps:
  - Dual logging channels coexist (`Logger` and `LicorpTrace`) without explicit policy.
  - No shared logging contract abstraction (e.g., `IAppLogger`) for services.
  - Inconsistent adoption across modules may complicate observability.

### 1.5 Build / Bundle / Package Scripts (As-Is)

#### `buildtest.bat`
- Strengths:
  - Simple target matrix and clear success/fail path.
  - Output copy excludes `publish/obj` to avoid recursive nesting.
- Improvement candidates:
  - Optional structured summary output (machine-readable) for CI-like checks.

#### `deploy-bundle.ps1`
- Strengths:
  - Clean runtime-to-year deployment map.
  - Strong validations (`Assert-BundleLayout`, manifest assembly checks).
  - Stale manifest cleanup and robust directory cleanup strategy.
- Improvement candidates:
  - Extract repeated constants/maps to shared script module for reuse.

#### `build-package.ps1`
- Strengths:
  - Reuses `deploy-bundle.ps1 -SkipDeploy` and packages staging zip.
  - Version metadata sourced from `Directory.Build.props`.
- Improvement candidates:
  - Share duplicated manifest/map logic with `deploy-bundle.ps1` (single source of truth).

---

## 2) Target Architecture (To-Be)

Aligned with requested direction (Nice3point / chuongmep / ricaun-io inspired):

1. **Clear Composition Root**
   - All service registrations centralized in startup/bootstrap modules.
   - UI creation via factories/launchers, not direct `new` inside command logic.

2. **Separation of Concerns**
   - Thin command/application shell.
   - Business/export orchestration moved to application services/use-cases.
   - UI code-behind minimized to presentation concerns.

3. **Consistent Logging & Error Policy**
   - Unified logging facade/policy across modules.
   - Exception handling conventions: actionable context + deterministic user feedback.

4. **Script Reliability & Reuse**
   - Shared deployment metadata and helper functions.
   - No duplicated version/runtime map across scripts.

5. **Incremental Safety**
   - Small batches, each batch with green build evidence.
   - Preserve behavior for core export flows unless covered by tests.

---

## 3) Migration Roadmap (Phased, No Big-Bang)

### Phase 1 — Foundation (Low Risk)
- Standardize logging and exception handling conventions.
- Define module boundary rules for Shared project (`Services`, `Views`, `Utils`, `Events`).
- Script dedup prep: identify shared constants/map functions.

### Phase 2 — DI & Composition Root (High Value / Medium Risk)
- Introduce centralized service registration.
- Add interfaces for core orchestration services (export/profile/report/scheduler).
- Refactor command to resolve a window launcher/factory from DI.

### Phase 3 — UI / Command / Event Refactor (Medium-High Risk)
- Move business logic out of `ExportPlusMainWindow.xaml.cs` into service/use-case layer.
- Keep code-behind as thin adapter.
- Normalize ExternalEvent responsibility boundaries.

### Phase 4 — Package & Dependency Alignment
- Centralize runtime/year mapping for scripts.
- Align package/dependency strategy while preserving Revit target compatibility.

### Phase 5 — Hardening & Verification
- Full build verification and deployment/package dry-runs.
- Warning triage with priority levels.
- Maintenance docs for team adoption.

---

## 4) Priority Modules for Refactor Execution
- `Source/LicorpExportPlus.Shared/ExportPlusApplication.cs`
- `Source/LicorpExportPlus.Shared/ExportPlusCommand.cs`
- `Source/LicorpExportPlus.Shared/Views/ExportPlusMainWindow.xaml.cs`
- `Source/LicorpExportPlus.Shared/Services/*`
- `buildtest.bat`
- `deploy-bundle.ps1`
- `build-package.ps1`

---

## 5) Immediate Next Step (Proposed)

Start **Phase 1 implementation batch A**:
1. Add architecture conventions doc (`docs/architecture-conventions.md`).
2. Introduce logging policy (single facade decision and usage rules).
3. Keep current behavior intact; run `buildtest.bat Release All` after changes.

> Note: per your instruction, implementation will stop at code + verification only, then hand over for your manual testing (**no commit, no push**).
