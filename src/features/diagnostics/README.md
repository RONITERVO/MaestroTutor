# Diagnostics Feature

The diagnostics feature handles debugging and logging functionality.

## Responsibilities

- Debug log display panel
- Log capture and storage
- Developer diagnostics tools

## Owned Store Slice

`diagnosticsSlice` - see `src/store/slices/diagnosticsSlice.ts`

### State
- `showDebugLogs`: Whether debug panel is visible

### Key Actions
- `setShowDebugLogs()`: Show/hide debug panel
- `toggleDebugLogs()`: Toggle debug panel visibility

## Public API

Import from `src/features/diagnostics/index.ts`:

```typescript
import { 
  DebugLogPanel,
  debugLogService,
} from '../features/diagnostics';
```

## Components

- `DebugLogPanel`: Floating debug log viewer

## Services

- `debugLogService.ts`: Log capture and retrieval

## STT Flow Logging

- Android STT/send pipeline tracing uses the native Logcat tag `MaestroSttFlow`.
- It is enabled while the in-app debug panel is open.
- It can also be forced on by setting `localStorage['maestro.sttFlowDebug'] = '1'`.
- This keeps the trace available for device-only audio bugs without leaving always-on production noise.

## Usage

The debug panel can be toggled from the Header component.
Logs are captured throughout the app using `debugLogService.log()`.
