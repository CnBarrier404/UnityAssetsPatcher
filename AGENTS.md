# AGENTS.md

UnityAssetsPatcher is a .NET 10 desktop application evolving into an alternative mod manager for Unity games with special modding requirements, particularly where runtime mod frameworks such as `BepInEx` are impractical. The application uses an Avalonia-based graphical interface for managing mod installation and uninstallation. It supports mod installation through file copies and direct Unity asset modifications described by a `manifest.json` inside each zip-based mod package.

## Commands

| Action | Command                                |
| :----- | :------------------------------------- |
| Build  | `dotnet build UnityAssetsPatcher.slnx` |
| Test   | `dotnet test UnityAssetsPatcher.slnx`  |

- Documentation-only changes: check content, links, and the diff; builds and tests are not required.
- Localized code changes: run the build command above and the affected existing tests.
- Changes spanning projects, dependencies, or build configuration: run both full-solution commands above. Full validation may also be used when the impact of a code change is unclear.
- After checks pass, repeat or broaden validation only when subsequent changes, failures, or unresolved concerns justify it.
- Do not add tests, including test files or test cases, unless the user explicitly requests them.

## Repository Structure

| Directory                                       | Responsibility                                                                                       |
| :---------------------------------------------- | :--------------------------------------------------------------------------------------------------- |
| `docs/`                                         | User and mod author documentation                                                                    |
| `schema/`                                       | Mod manifest schema                                                                                  |
| `src/UnityAssetsPatcher/`                       | Executable entry point and composition root; owns dependency registration and concrete logging setup |
| `src/UnityAssetsPatcher.Application/`           | Use cases, workflow orchestration, DTOs, and infrastructure abstractions                             |
| `src/UnityAssetsPatcher.CLI/`                   | Non-interactive command parsing and text/JSON presentation                                           |
| `src/UnityAssetsPatcher.Domain/`                | Domain models, value objects, validation rules, and domain errors                                    |
| `src/UnityAssetsPatcher.Infrastructure/`        | File system, compression, persistence, backup, package, and AssetsTools.NET implementations          |
| `src/UnityAssetsPatcher.LocalizationGenerator/` | Roslyn source generator for strongly typed localized strings                                         |
| `src/UnityAssetsPatcher.TUI/`                   | Interactive terminal UI and localization inputs                                                      |
| `tests/`                                        | Unit and integration tests                                                                           |

## Workflows

1. Inspect only the code and documentation relevant to the task. For bugs, support the diagnosis with code or reproduction evidence and distinguish hypotheses from confirmed causes.
2. Present the intended behavior, scope, and validation plan. Obtain explicit approval before editing implementation files.
3. Before approval, read-only inspection, local builds, and verified isolated tests or reproductions are allowed, including disposable diagnostic artifacts. Operations involving real game files or user data require separate authorization.
4. Once approved, complete implementation, validation, and fixes caused by the change without repeated approval. Ask again only for material changes to requirements, observable behavior, architecture, or impact.
5. Do not add test files or test cases unless explicitly requested. Run existing tests as appropriate and report coverage gaps.
6. Follow the validation rules under [Commands](#commands). Continue until the agreed behavior and applicable checks pass; report evidence and remaining work when unrelated failures or external blockers prevent completion.

### Infrastructure

- Infrastructure implementations must propagate original exceptions from underlying platform APIs and third-party libraries without wrapping, translating, or replacing them.

### Error Handling

The exception policy follows the [.NET best practices for exceptions](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions) and [CA1031](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1031). Exceptions are divided into expected operational failures, cancellation, and unexpected exceptions. These categories must not be inferred from timing or from a broad `catch`; they are determined by an explicit contract and by ownership of the operation.

#### Expected Failures

- Expected operational failures must be represented by `OperationFailed` and handled as ordinary control flow by callers.
- A failure is expected only when the application has deliberately defined its meaning and recovery or presentation behavior. The fact that an exception came from I/O, a third-party library, or user input does not by itself make it expected.
- Catch only specific exception types whose meaning is known at that layer and whose conversion to an application error is intentional. Do not use `catch (Exception)` to manufacture an `OperationFailed` result for an unknown defect.
- Infrastructure implementations must propagate original exceptions. The application layer is responsible for translating known infrastructure failures into `OperationFailed` results.
- Cleanup must use `using`, `await using`, or `finally` whenever possible. If a broad catch is required to roll back or restore state, it must rethrow with `throw;` after restoring the invariant. If rollback also fails, the propagated exception must preserve both failures rather than discard the original one. See [how to use `finally` blocks](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/how-to-use-finally-blocks) and [how to rethrow exceptions without losing stack information](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions#capture-and-rethrow-exceptions-properly).

#### Cancellation

- Cancellation is cooperative control flow, not an operational failure or an unexpected error. Use `CancellationToken`, propagate it through every cancellable call, and use `ThrowIfCancellationRequested()` when the operation itself must acknowledge cancellation. See [.NET task cancellation](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation) and [recommended `CancellationToken` patterns](https://devblogs.microsoft.com/premier-developer/recommended-patterns-for-cancellationtoken/).
- Intermediate layers must normally allow `OperationCanceledException` to propagate. They may catch it to restore state, but must then rethrow it with `throw;`.
- The component that creates the operation's `CancellationTokenSource`, or the lifecycle boundary explicitly given control of that source, owns the operation's cancellation. Receiving a token as a method parameter does not by itself make that method the owner.
- Only the lifetime owner may consume cancellation. It must verify both that its token was requested and that the exception carries the token for that operation: `catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested && exception.CancellationToken == cancellationToken)`. Integrations that cannot preserve token identity require a narrowly scoped, documented adapter; cancellation timing alone is not sufficient evidence.
- Never use `catch (Exception) when (cancellationToken.IsCancellationRequested)`. A cancellation request does not prove that an unrelated exception was caused by cancellation.
- Cancellation must not leave state inconsistent. After an operation has passed its point of no cancellation or has completed its side effects, it must finish or roll back rather than report cancellation merely because the token was subsequently signaled.
- An `OperationCanceledException` that is not attributable to the owning token must follow the expected- or unexpected-error policy appropriate to its known cause; it must not be silently treated as caller cancellation.

#### Unexpected Exceptions

- Lower layers must allow unexpected exceptions to propagate unchanged. They must not swallow them, convert them into `OperationFailed`, or log and rethrow them.
- `Program.Main` is the single process-level unexpected-exception boundary. Its protected execution includes bootstrap and dependency composition, CLI or TUI execution, and asynchronous disposal. An unexpected exception reaching this boundary is recorded once, receives generic user-facing output, selects a non-zero exit code, and terminates the application. The application must not attempt to continue.
- The logging implementation used by this boundary must be owned outside the dependency container lifetime it reports on, so failures from host execution or asynchronous container disposal can still be recorded before logging is flushed and disposed.
- `CLIApplication`, `TerminalApp`, sessions, commands, views, page logic, application handlers, and infrastructure services are not final unexpected-exception boundaries. They must not log, present, or convert an unexpected exception merely because they are the outermost type in their project or feature.
- A general `catch (Exception)` below `Program.Main` is permitted only when it is necessary to restore an invariant or roll back, perform best-effort cleanup before rethrowing, or transfer the same failure into an awaited completion mechanism such as `TaskCompletionSource`. Such a catch must not classify the failure as expected, continue normal execution, or create a duplicate log entry. When an exception must cross a callback boundary, preserve its traceback with `throw;` or [`ExceptionDispatchInfo`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.exceptionservices.exceptiondispatchinfo).
- `Program.Main` may consume an `OperationCanceledException` as normal process cancellation only if the entry point owns the matching process-lifetime token. Otherwise an escaped cancellation exception is handled by the unexpected-exception boundary.
- User-facing unexpected-error output must not expose stack traces, exception messages, paths, or other internal details. Complete diagnostic detail belongs only in the single boundary log entry.
- `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` are diagnostic fallbacks, not application error-handling boundaries. An unhandled exception notification can occur while the process is terminating and is not a safe recovery point. See [.NET guidance for `AppDomain.UnhandledException`](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-appdomain-unhandledexception) and [`TaskScheduler.UnobservedTaskException`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception).

#### Asynchronous Exception Propagation

- Every asynchronous operation that can fail must return a `Task` or `Task<T>` and must be awaited or otherwise explicitly observed by the component that owns its lifetime. Exceptions from asynchronous methods are stored in their tasks and propagate when those tasks are awaited. See [asynchronous exception handling](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/#handle-asynchronous-exceptions).
- Fire-and-forget tasks that can fault are prohibited. Keeping a task in a field is sufficient only when the owning lifecycle reliably awaits it and propagates its failure. Waiting only for eventual disposal is insufficient when an unexpected failure is required to terminate the currently running application; the failure must be connected promptly to that lifecycle.
- `async void` is permitted only for genuine event handlers. The handler must contain minimal adapter code and await a `Task`-returning operation that holds the testable workflow. A caller cannot await an `async void` method or catch exceptions from it directly; those exceptions are raised through the current `SynchronizationContext`. See the guidance for [async return types](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-return-types#void-return-type).
- For the pinned Terminal.Gui `2.4.18-develop.53` version, `MainLoopSyncContext` posts asynchronous continuations back through the application loop, and `IApplication.RunAsync` with no error handler rethrows an unhandled loop exception through its returned task. The session must await that task, and no error handler may resume the loop after an unexpected exception. See the package's pinned commit sources for [`MainLoopSyncContext`](https://github.com/tui-cs/Terminal.Gui/blob/48efa0c5c005763de035f2ee4afed28b7df98db3/Terminal.Gui/App/MainLoop/MainLoopSyncContext.cs) and [`ApplicationImpl.RunAsync`](https://github.com/tui-cs/Terminal.Gui/blob/48efa0c5c005763de035f2ee4afed28b7df98db3/Terminal.Gui/App/ApplicationImpl.Run.cs).
- Every callback-to-task bridge must complete for success, cancellation, and failure. Transferring an unexpected exception into an awaited `TaskCompletionSource` is propagation, not handling; the receiving lifecycle must still terminate and pass it to `Program.Main`.
- An unexpected exception from background work must be connected to the awaited application lifecycle and ultimately reach `Program.Main`. Catching and swallowing it merely to keep the UI or command running violates this policy.

### Application Configuration

- `AppConfig` is the static source for application identity, version, and fixed application directories.
- `AppRuntimeConfig` is the singleton source for mutable process-level settings such as verbose logging.
