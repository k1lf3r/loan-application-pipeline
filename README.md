# LoanApplicationPipeline - Assignment 8: Extension Methods and LINQ

Introduction to Functional Programming, 2026-2027. One runnable project across Assignments 5-8.
Assignment 8 adds: custom extension methods, two LINQ reports, curried scoring, deferred execution,
safe disposal, a retention note, and a native C library called through P/Invoke.

## Team

| Member | Group | Implemented logic | Tests | Documentation |
|---|---|---|---|---|
| **Yernazar Zhauken** | ______ | Tasks 1, 2, 3 + integration: `ResultExtensions.Bind`, `ApplicationQueryExtensions` (`Scored`, `TotalRequested`), `ApplicationReports` (both LINQ reports), `ScoringFunctions` (currying), `LoanReportWorkflow`, `ReportExporter`, `Program.cs` | `ReportAndExtensionTests.cs` (11 methods), `WorkflowAndFileTests.cs` (8 methods) | Sections "Reports", "Extensions", "Configured functions", "Integration", "Expected results" |
| **Dias Yergazi** | ______ | Tasks 4-9: `ApplicationFile` (yield + `using`), CSV parsing (`ApplicationCsv`), native library `native/loanrisk.c/.h`, build scripts, `NativeMethods`, `NativePaymentCalculator` adapter | `LazyAndResourceTests.cs` (7 methods), `NativeIntegrationTests.cs` (4 methods) | Sections "Deferred execution", "Resource ownership", "Resource note", "Native library" |

(Fill in the Group column before submission. Each member defends their own rows.)

## Requirements

- .NET SDK 10.x (`dotnet --version`), a C compiler: `gcc` (MinGW-w64 on Windows) or MSVC `cl`.
- Run all commands from the repository root (the folder that contains this README).

```text
# 1. build the native library (once, and again after changing native/*.c)
Linux/macOS :  sh native/build.sh
Windows     :  native\build.bat          (MinGW-w64 gcc on PATH)
                or, MSVC x64 prompt:  cl /nologo /O2 /LD /Fe:native\bin\loanrisk.dll native\loanrisk.c

# 2. tests
dotnet test tests/LoanApplicationPipeline.Tests

# 3. normal demonstration (deferred demo, both reports, both policies, CSV export to ./output)
dotnet run --project src/LoanApplicationPipeline

# 4. failure demonstrations
dotnet run --project src/LoanApplicationPipeline -- fail-native   # native load failure -> Result failure, exit code 1, nothing exported
dotnet run --project src/LoanApplicationPipeline -- fail-file     # missing input file -> Result failure, exit code 1
```

## Input and domain rules

Input: `src/LoanApplicationPipeline/Data/applications.csv` (10 loan applications, copied next to the executable).
Columns: `id,name,purpose,amount,termMonths,annualRateBp,monthlyIncome,creditScore,existingDebt`.
Money is in whole tenge; rates are basis points (1200 = 12.00 %).

`LoanApplication.Create` validates: id and name not empty; purpose is one of Education, Housing, Business, Consumer;
amount 1..10^12; term 1..600; rate 0..10000 bp; income > 0; credit score 300..850; existing debt >= 0.
Every violation is an explicit `Result` failure with a code such as `domain.invalid_credit_score`.

Pricing: the native function returns the monthly annuity payment.
`PaymentToIncomeBp = (payment + existingDebt) * 10000 / income`, `BurdenPercent = PaymentToIncomeBp / 100`.

## Task 1 - Custom extension methods (Yernazar)

| Extension | Receiver -> return | Why it belongs on the receiver | Used in |
|---|---|---|---|
| `Bind` (`Common/ResultExtensions.cs`) | `Result<TIn>` -> `Result<TOut>` | Chaining is an operation *of* a Result: success continues, failure is passed on and the next step is skipped. | `LoanReportWorkflow.Prepare`, `.Run` |
| `Scored` (`Queries/ApplicationQueryExtensions.cs`) | `IEnumerable<PricedApplication>` -> `IEnumerable<ScoredApplication>` (lazy) | Scoring is a projection of a sequence of priced applications; both reports need it, so it is written once. The score function is a parameter, so no global setting. | both reports |
| `TotalRequested` (same file) | `IEnumerable<PricedApplication>` -> `long` (uses `Aggregate`) | A total over applications is a reusable aggregate; empty input gives 0. Read-only. | Report 2, `BuildReport` |

## Task 2 - Two LINQ reports (Yernazar)

Both live in `Queries/ApplicationReports.cs`, are pure (no Console, no file, no native call) and take the data
loaded from `applications.csv`.

**Report 1 - Manual review queue.** User: credit underwriter. Input: priced applications, a configured score
function, a threshold (500). Filter: `score < threshold`. Pipeline: `Scored -> Where -> OrderBy(score) ->
ThenBy(Id, ordinal) -> Select -> ToList`. Output fields: `Id, Name, BurdenPercent, Score`.
Expected (sample data): Conservative -> `A006, A004, A009, A008, A002, A010`; Flexible -> `A006`.

**Report 2 - Approved portfolio by purpose.** User: portfolio manager. Same inputs. Filter: `score >= threshold`.
Pipeline: `Scored -> Where -> GroupBy(purpose) -> Select(Count, TotalRequested/Aggregate, Sum(payment)) ->
OrderByDescending(total) -> ThenBy(purpose) -> ToList`. Output: `Purpose, Count, TotalAmount, TotalMonthlyPayment`.
Expected: Conservative -> Housing 2 / 15 000 000 / 278 816 and Education 2 / 3 000 000 / 109 508.
Flexible -> Housing 3 / 35 000 000 / 525 320, Business 2 / 14 000 000 / 302 759, Education 2 / 3 000 000 / 109 508,
Consumer 2 / 1 800 000 / 126 402.

Ties are deterministic: Report 1 breaks equal scores by `Id` (ordinal); Report 2 breaks equal totals by the `LoanPurpose` order.

## Task 3 - Curried function and partial application (Yernazar)

`ScoringFunctions.CreateScore : creditWeight -> burdenWeight -> (PricedApplication -> int)`,
`Score = CreditScore * creditWeight - BurdenPercent * burdenWeight`.
Partial application: `Conservative = CreateScore(1)(10)`, `Flexible = CreateScore(1)(4)`.
Same input (credit 700, burden 30 %): Conservative 400, Flexible 580, so with threshold 500 one is sent to review
and the other is not. The configured function is passed into `BuildReport`/`ReviewQueue`; there is no mutable global.

## Task 4 - Deferred execution (Dias)

`ApplicationFile.Stream(Func<TextReader>)` uses `yield return` and produces one parsed `Result<LoanApplication>`
per CSV line. Creating the enumerable runs none of the body (the reader is not even opened); the first `MoveNext`
opens the reader. Tests count opens through the `open` delegate (a probe outside the production code).
`ApplicationFile.Load` is the deliberate materialisation point (`ToArray`): before it, each enumeration would
re-open and re-read the file (test: 3 opens after 1 `ToArray` + 2 more passes); after it, the snapshot can be
enumerated any number of times with no file access and no repeated native calls.

## Task 5 - Resource ownership and disposal (Dias)

| Resource | Owner | Release point | Failure/early-stop path |
|---|---|---|---|
| `TextReader` from `open()` in `ApplicationFile.Stream` | the iterator | `using var reader` | disposed when iteration ends, the consumer stops early (`Take`, `break`) or throws (`foreach` disposes the enumerator) |
| `StreamReader` created in `StreamFile` / `Load(path)` | `Stream` (it receives and owns it) | same `using` | read error -> `IOException` caught in `Load` -> `Fail("file.unreadable")` after disposal |
| `StreamWriter` in `ReportExporter.Export` | `Export` | `using` block that ends **before** `File.Move` | temp file deleted, existing report left untouched, `Fail("file.export_failed")` |

No convenience API (`File.ReadAllText` etc.) is used for owned resources; the tests only use it for setup.
Garbage collection is not relied on to release file handles.

## Task 6 - Resource note (retention and allocation) (Dias)

- **Materialisation:** `ApplicationFile.Load` (arrays of results, then `LoadedApplications`) and `LoanReportWorkflow.Price`
  (`PreparedPortfolio.Priced`). Reports end with `ToList()`, so the returned rows hold no reference to the source query.
- **Deferred queries / closures:** `Scored` captures its source and the score delegate only while it is being enumerated;
  nothing stores it. `Conservative`/`Flexible` are closures over two integers, they hold no application data.
- **Long-lived collections, caches, event subscriptions:** none are used. There are no static collections, no caches and no events.
- **Design decision:** the file is read and the native payment computed **once** into a snapshot, and both policies
  build their reports from that same snapshot. This avoids re-reading the file and repeating native calls per report.
- **Trade-off:** streaming keeps only one record in memory but keeps the file open for the whole processing and can be
  enumerated once; a snapshot costs memory proportional to the number of records but closes the file immediately,
  allows reuse and gives repeatable results. For 10 records (or thousands) the snapshot is the right choice;
  for a file too large for memory, the reports would need a single streaming pass. No leak was found or fixed.

## Tasks 7-9 - Native C library (Dias)

Files: `native/loanrisk.c`, `native/loanrisk.h`, `native/build.sh`, `native/build.bat`, binary in `native/bin/`.
C# side: `Native/NativeMethods.cs` (`DllImport`), `Native/NativePaymentCalculator.cs` (adapter).

**Function contract**

```c
int64_t lp_monthly_payment(int64_t principal_tenge, int32_t annual_rate_bp, int32_t term_months);
```

| Item | Value |
|---|---|
| C# binding | `[DllImport("loanrisk", EntryPoint="lp_monthly_payment", CallingConvention=Cdecl)] static extern long MonthlyPayment(long, int, int)` |
| Input types | `int64_t` = C# `long`; `int32_t` = C# `int` (fixed-width types, no platform-dependent `long`) |
| Valid range | principal 1..1 000 000 000 000 tenge; rate 0..10000 bp; term 1..600 months |
| Output | monthly payment in whole tenge, rounded half away from zero (`llround`); amounts are whole tenge, no floating currency |
| Invalid input | returns a negative code: `-1` principal, `-2` rate, `-3` term (the adapter checks the same ranges first, so these are a second line of defence) |
| Formula | `r = bp / 10000 / 12`; `payment = P*r / (1 - (1+r)^-n)`; if `bp = 0`: `P / n` |
| Manual example | P = 1 000 000, 1200 bp, 12 months: r = 0.01; 10 000 / (1 - 1.01^-12) = 10 000 / 0.112551 = 88 848.79, so **88 849** |
| Memory | simple values only; no native allocation, nothing to free |

**Adapter (`NativePaymentCalculator`).** Validates domain input -> calls C -> negative code becomes
`native.rejected_input`; `DllNotFoundException` -> `native.load_failed`; `EntryPointNotFoundException` ->
`native.entry_point_missing`; `BadImageFormatException` -> `native.wrong_architecture`. The workflow treats the last three as
fatal (the whole workflow returns a `Fail`, no report is exported); `native.rejected_input` rejects only that record.
A second constructor takes the native call as a `Func`, which lets tests inject a failing call.

**Build and loading**

| Platform | Compiler | Command | Output |
|---|---|---|---|
| Linux x86_64 (verified while preparing this project) | gcc 13.3.0, Ubuntu 24.04 | `sh native/build.sh` = `gcc -std=c99 -O2 -Wall -Wextra -shared -fPIC -o native/bin/libloanrisk.so native/loanrisk.c -lm` | `native/bin/libloanrisk.so` |
| Windows x64 | MinGW-w64 gcc: ______ (`gcc --version`) | `native\build.bat` = `gcc -std=c99 -O2 -Wall -Wextra -shared -static-libgcc -o native\bin\loanrisk.dll native\loanrisk.c` | `native\bin\loanrisk.dll` |

Both `.csproj` files copy whichever binary exists in `native/bin/` next to the executable / test assembly
(`<None ... CopyToOutputDirectory="PreserveNewest">`), and .NET resolves `"loanrisk"` to `loanrisk.dll`,
`libloanrisk.so` or `libloanrisk.dylib` in the application folder. Target architecture must match the .NET runtime
(x64 library for an x64 `dotnet`). After a clean build, `dotnet build` copies the binary again.

**Integration difficulties and how they were handled** (replace with your own experience if it differs):
1. On Windows a DLL exports nothing unless the function is marked `__declspec(dllexport)`; `loanrisk.h` defines
   `LOANRISK_API` for this, and `build.bat` + an `EntryPointNotFoundException` mapping make a missing export visible.
2. C `long` is 32-bit on Windows and 64-bit on Linux, so the signature uses `int64_t`/`int32_t`, matching C# `long`/`int`.
3. Deleting a loaded library does not test a fresh load failure, so the failure path is tested through the injectable
   adapter, and the real call is tested separately against the manually calculated value 88 849.

## Integration (Yernazar)

`LoanReportWorkflow.Prepare` = `ApplicationFile.Load(open).Bind(loaded => Price(loaded, calculator))` (file + native,
outside pure queries). `BuildReport` is pure. `Program` runs both reports for both policies on one snapshot and exports
`output/report_conservative.csv` and `output/report_flexible.csv` through `ReportExporter` (temp file + move).
Failure path: a native load failure or a missing file returns a `Fail`, prints the code, exit code 1, and exports nothing;
a failed export keeps the previous file. Earlier assignments' parts (pure pipeline stages, safe states, `Result`
failures) are kept: bad records become explicit `Error` values, never exceptions.

## Requirement map

| Requirement | Files / methods |
|---|---|
| 2 extensions used | `ResultExtensions.Bind`, `ApplicationQueryExtensions.Scored/TotalRequested` |
| 2 LINQ reports | `ApplicationReports.ReviewQueue`, `ApplicationReports.ApprovedByPurpose` |
| Currying + 2 partial applications | `ScoringFunctions.CreateScore/Conservative/Flexible` |
| Deferred execution | `ApplicationFile.Stream`, `ApplicationFile.Load` |
| Disposal | `ApplicationFile.Stream` (`using`), `ReportExporter.Export` (`using`) |
| Retention note | section "Task 6" above |
| Native C library | `native/loanrisk.c/.h`, `NativeMethods`, `NativePaymentCalculator` |
| Complete workflow | `LoanReportWorkflow`, `Program.cs` |

## Tests (30 test methods, 37 cases with the theory; minimum 12)

| Area | Test methods (file) | Expected result |
|---|---|---|
| Reports | `ReviewQueue_*`, `ApprovedByPurpose_*` (ReportAndExtensionTests) | normal rows, empty input, no matches, ties by Id / purpose, grouping and totals |
| Extensions | `Scored_IsLazy_*`, `TotalRequested_*`, `Bind_*` | lazy projection, sum / 0 for empty, failure skips the next step |
| Configured functions | `PartiallyApplied_Scorers_*`, `ConfiguredFunction_ChangesReportOutcome_*` | 400 vs 580 on the same input; review vs no review |
| Domain validation | `Create_RejectsInvalidValues_*` (8 cases), `Create_RejectsUnknownPurpose_*` | specific `domain.invalid_*` codes |
| Deferred execution | `Stream_DoesNothingUntilEnumerated_*`, `Materialisation_*` (LazyAndResourceTests) | 0 opens before iteration; 1 after `ToArray`; reuse = no new opens |
| Resource lifetime | `Stream_DisposesReader_*` (success, early stop, consumer throws), `Load_WhenReadingFailsMidway_*`, `Load_FromFilePath_*` | reader disposed; file handle released |
| Native integration | `RealNativeCall_*`, `Adapter_*` (NativeIntegrationTests) | 88 849 and 100 000 from real C; load/binding failures become `Fail` |
| Complete workflows | `Workflow_NormalPath_*`, `Workflow_BadRecords_*`, `Workflow_NativeRejectsEverything_*`, `Workflow_NativeLoadFailure_*` | reports as listed above; failure keeps state consistent |
| File boundaries | `Load_MissingFile_*`, `Load_MalformedContent_*`, `Export_*` (temp dirs, cleaned up) | safe failures, rejected rows, no `.tmp` left, old file kept |

**Latest test outcome:** ______ (run `dotnet test tests/LoanApplicationPipeline.Tests` from a clean extraction and paste
the result here, e.g. "Passed: 37, Failed: 0, date").
