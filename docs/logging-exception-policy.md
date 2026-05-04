# Logging & Exception Policy (Phase 1 Foundation)

## Objective
Standardize diagnostics behavior across startup, command, services, and UI boundaries without changing export business behavior.

## 1) Logging Channel Policy

Current state has both `LicorpTrace` and `Logger` usage. During migration:

- **Boundary logs** (startup/command/revit integration) should prefer `LicorpTrace` for continuity.
- **Service/internal workflow logs** should gradually converge to a single facade policy.
- No new ad-hoc logging utility should be introduced.

### Transitional Rule (safe)
- Existing logs stay as-is unless touched by a refactor batch.
- Any updated method must keep or improve log context quality.

## 2) Minimum Log Context Requirements
At warning/error level, include:
- operation name (e.g. `ExportProfiles`, `SaveXmlProfile`, `LoadSheetBatch`)
- key target/context (profile name, sheet set, output folder, file path)
- exception object for failures (`ex`), not only `ex.Message`

## 3) Exception Handling Rules

### Catch Location
Catch exceptions at boundaries:
- Revit entry points (`OnStartup`, `Execute`)
- External events
- file/network/serialization operations

Avoid broad catch blocks deep in pure helper logic unless there is a recovery strategy.

### User Feedback
- UI-facing message should be concise and actionable.
- Technical details go to logs.
- Never expose raw stack traces directly in user dialogs unless explicitly requested.

### Rethrow/Wrap Guidance
- If adding context, wrap with operation metadata and preserve inner exception.
- If boundary already logs and returns status, avoid duplicate noisy logging deeper down.

## 4) Levels Guidance
- `Debug/Trace`: detailed diagnostics for development-only flow.
- `Info`: lifecycle milestones and major operations start/finish.
- `Warning`: recoverable anomaly or fallback behavior.
- `Error`: operation failed or command/application failed.

## 5) Anti-Patterns to Avoid
- Empty catch blocks.
- Logging only `ex.Message` when exception object is available.
- Logging same error repeatedly across multiple layers without added context.

## 6) Adoption Plan
1. Apply policy to newly refactored modules first.
2. During Phase 2/3, introduce a unified app logger facade if needed.
3. Keep behavior unchanged while increasing observability quality.
