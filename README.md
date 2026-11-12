# Loan Application Pipeline

Console application in C# implementing a functional loan underwriting and portfolio reporting pipeline.

## Repository & Submission Metadata

* **Repository:** https://github.com/k1lf3r/loan-application-pipeline
* **Branch:** `main`
* **Final Commit:** `df6ca1ac76b6a81a3accee67980ec8b960c7ad96`

## Prerequisites

* **.NET SDK:** .NET 8.0 or .NET 9.0 (`dotnet --version`)
* **Native Library (`loanrisk.dll`):** Precompiled binary is included in `native/bin/loanrisk.dll`. To rebuild from C sources on Windows (GCC/MinGW or MSVC), run `native/build.bat`.

## Build, Run, and Test

Run all commands from the repository root (`LoanApplicationPipeline/`):


# Restore and build solution
dotnet build LoanApplicationPipeline.slnx

# Run console workflows and generate reports
dotnet run --project LoanApplicationPipeline/LoanApplicationPipeline.csproj

# Execute automated test suite
dotnet test LoanApplicationPipeline.Tests/LoanApplicationPipeline.Tests.csproj


## Project Structure


LoanApplicationPipeline/
├── LoanApplicationPipeline.slnx
├── native/                          # C risk calculation library (loanrisk.c/.h, build.bat, loanrisk.dll)
├── LoanApplicationPipeline/
│   ├── Core/                        # Result<T, E>, Option<T>, DomainError, ResultExtensions (Map/Bind)
│   ├── Models/                      # Immutable records, LoanWorkflowState, staged pipeline types
│   ├── Scoring/                     # Higher-order scoring rules and configurable delegates
│   ├── Pipeline/                    # Staged transformations, approval policies, single & portfolio workflows
│   ├── Native/                      # P/Invoke bindings (NativeMethods) and NativePaymentCalculator
│   ├── Queries/                     # Custom IEnumerable/LINQ extensions and grouped aggregation reports
│   ├── Services/                    # JSON/CSV boundary readers, registry, and report exporter
│   ├── Data/                        # Sample datasets (applicants.json, applications.csv, applications_with_errors.csv)
│   └── Program.cs                   # CLI entry point demonstrating workflows and reports
└── LoanApplicationPipeline.Tests/   # Automated tests (pure core, state sequence, workflows, I/O, native interop)


## Functional Design Mapping

| Requirement | Implementation |
| :--- | :--- |
| **Functional Core & Immutability** | `Models/*` (immutable records), pattern matching in `Pipeline/ApprovalPolicies.cs` and `Scoring/ScoringFunctions.cs` |
| **Composition & Higher-Order Functions** | `Pipeline/ApplicationStages.cs`, `Pipeline/LoanApplicationWorkflow.cs`, `Scoring/ScoringFunctions.cs` (`Func` rule injection) |
| **Safe Operation Sequence** | Type-driven stage progression (`RawApplicationRecord` -> `ValidatedApplication` -> `PricedApplication` -> `PolicyDecision` -> `DecisionReceipt`) and `Models/LoanWorkflowState.cs` |
| **Functional Error Handling** | `Core/Result.cs` (`Map`, `Bind`), `Core/Option.cs` (`ApplicantRegistry`), boundary exception catching in `Services/*` |
| **Queries & Lazy Processing** | `Queries/ApplicationQueryExtensions.cs`, `Queries/ApplicationReports.cs` (`GroupBy`, tie-breaker sorting, `yield return` streaming) |
| **Persistence & Native Interop** | `Services/ApplicantFile.cs`, `Services/ApplicationCsv.cs`, `Services/ReportExporter.cs`, `Native/NativePaymentCalculator.cs` |

## Team Responsibilities

| Member | GitHub / Email | Owned Modules & Tests |
| :--- | :--- | :--- |
| **Dias Yelgazy** | `k1lf3r` (`ikillfera@gmail.com`) | Functional core (`Core/`), immutable domain models (`Models/`), scoring & pipeline workflows (`Scoring/`, `Pipeline/`), C native library & interop (`native/`, `Native/`), core/pipeline/native tests |
| **Zhauken Yernazar** | `zhaukene-rgb` (`zhaukene@gmail.com`) | Boundary I/O & persistence (`Services/`, `Data/`), LINQ reports & lazy `yield return` extensions (`Queries/`), CLI entry point (`Program.cs`), documentation (`README.md`), boundary/lazy/report tests |