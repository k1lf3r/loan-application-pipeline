LOAN APPLICATION PIPELINE — ASSIGNMENTS 6 & 7 SUMMARY
1. PROJECT OVERVIEW & ARCHITECTURE
Topic: Loan application processing and scoring pipeline.
Academic Year: 2026-2027
Team: Yernazar, Dias
Platform: .NET 10 (C# 13/14), xUnit, Visual Studio 2026 / VS Code
Solution: LoanApplicationPipeline.slnx

This stage combines the solutions for Assignment 6 (eliminating temporal coupling, validated factory, explicit states) and Assignment 7 (monadic error handling with Result and Option, Map/Bind composition, and boundary exception handling).

Input Fields:

ApplicantId (string): Unique applicant identifier.
MonthlyIncome (decimal): Monthly income, must be non-negative.
RequestedAmount (decimal): Loan amount requested, must be strictly positive.
CreditScore (int): Standard credit rating, range [300, 850].
HasExistingDebt (bool): Must parse strictly to true or false.
Domain Policies:

Eligibility: Application must have CreditScore >= 500 to be admitted for review.
Conservative Policy: Requires CreditScore >= 650. Base limit is 3x income (halved if applicant has debt).
Flexible Policy: Requires CreditScore >= 550. Base limit is 5x income (halved if applicant has debt).
2. BUILD AND EXECUTION INSTRUCTIONS
Prerequisites:

.NET 10 SDK (verify using command: dotnet --list-sdks)
Expected working directory: repository root (where LoanApplicationPipeline.slnx is located).
Terminal Commands:

Build:
dotnet build LoanApplicationPipeline.slnx

Run Automated Tests:
dotnet test LoanApplicationPipeline.slnx

Run Console Demonstration:
dotnet run --project LoanApplicationPipeline/LoanApplicationPipeline.csproj

3. ASSIGNMENT 6: HANDLING TEMPORAL COUPLING
Task 1: Unsafe Before Example
In a procedural approach, application lifecycle correctness depends on an implicit method execution order:

// UNSAFE CODE (Before):
var app = new LoanApplication();
app.MonthlyIncome = -500;
app.RequestedAmount = 100000;
// Developer forgets app.Validate() or calls Approve() before Validate():
app.Approve(); // Erroneously succeeds with invalid data!
app.Validate();

Problem Identified:
The application can enter a terminal approved state without running validation checks. The caller has to remember an unwritten ordering contract.

Safe Solution (After):

Validated Factory: ValidatedApplication has a private constructor. Unchecked instantiation is prevented at compile time. External callers must call ValidatedApplication.Create(raw), which validates all domain invariants and returns Result<ValidatedApplication, DomainError>.
Explicit State Hierarchy:
LoanWorkflowState.PendingReview (holds ValidatedApplication and ApplicantProfile)
LoanWorkflowState.Approved (terminal state)
LoanWorkflowState.Rejected (terminal state)
Safe State Transitions: ApplicationStages.ApplyDecision accepts only state transitions defined in pattern matching. Calling ApplyDecision on already finalized states (Approved or Rejected) safely returns DomainError.InvalidStateTransition without corrupting data or throwing exceptions.
4. ASSIGNMENT 7: FUNCTIONAL ERROR HANDLING
Task 1: Explicit Domain Failures
Domain operations do not throw business exceptions. Failures are represented via the closed DomainError hierarchy:

ValidationError: Rejection by factory rules (e.g. negative income, invalid score bounds).
ApplicantNotFound: Applicant ID does not exist in registry.
IneligibleCreditScore: Credit score is below 500 threshold.
InvalidStateTransition: Attempt to re-evaluate an already finalized state.
Task 2: Missing Values and Legitimate Zero
The ApplicantRegistry.FindById method returns Option<ApplicantProfile> (Some or None).

Distinguishing Zero vs Absence: Applicant A1 has ExistingDebtAmount = 0m. This is valid numeric data returned as Option.Some, distinct from an absent record (Option.None).
If an applicant is not found, Option.None is converted into DomainError.ApplicantNotFound via .ToResult().
Task 3: Composition via Map and Bind
LoanApplicationWorkflow.ProcessApplication composes the end-to-end pipeline:

ValidatedApplication.Create(raw) returns Result<ValidatedApplication, DomainError>.
.Bind(...) performs lookup in ApplicantRegistry and packages the PendingReview state.
.Bind(ApplicationStages.VerifyEligibility) checks the 500 score threshold.
.Bind(...) evaluates the selected approval policy.
.Map(ApplicationStages.ToReceipt) transforms the terminal domain state into a DecisionReceipt.
Failure Propagation Guarantee:
If any step returns Failure, subsequent Map and Bind callbacks are not executed. This is verified in FunctionalCoreTests.FailurePropagation_MapAndBind_DoNotInvokeSuppliedFunctions using invocation counter assertions (counter remains 0).

Task 4: Boundary Exception Handling
ApplicationDataLoader.LoadFromJsonFile handles external I/O:

Missing file: System.IO.FileNotFoundException is intercepted and converted to DomainError.FileNotFound.
Malformed content: System.Text.Json.JsonException is intercepted and converted to DomainError.MalformedFile.
Domain state remains untouched and no unhandled runtime exceptions escape to the user.
5. REQUIREMENT-TO-CODE MAP
Core Monads (Option, Result):
Files: Core/Option.cs, Core/Result.cs
Author: Yernazar

Domain Error Model:
File: Core/DomainError.cs
Author: Dias

Validated Factory (Assignment 6):
File: Models/ValidatedApplication.cs
Author: Yernazar

Workflow State Discriminated Union:
File: Models/LoanWorkflowState.cs
Author: Dias

Registry with Option (Assignment 7):
File: Services/ApplicantRegistry.cs
Author: Yernazar

Boundary File Loader (JSON I/O):
File: Services/ApplicationDataLoader.cs
Author: Dias

Pure Pipeline Stages & Policies:
Files: Pipeline/ApprovalPolicies.cs, Pipeline/ApplicationStages.cs
Author: Dias & Yernazar

Workflow Composition (Map/Bind):
File: Pipeline/LoanApplicationWorkflow.cs
Author: Dias

Tests:
Files: FunctionalCoreTests.cs, FactoryAndTemporalTests.cs (Yernazar);
PipelineWorkflowTests.cs, BoundaryIoTests.cs (Dias).

6. AUTOMATED TEST SUITE (14 TESTS)
Test Class: FunctionalCoreTests

Map_OnSuccess_TransformsValue -> Verifies Map success transformation.
Bind_OnSuccess_ChainsDependentResult -> Verifies Bind continuation.
FailurePropagation_MapAndBind_DoNotInvokeSuppliedFunctions -> Verifies skipped dependent calls on failure using invocation counters.
Option_DistinguishesBetweenZeroAndAbsence -> Asserts legitimate debt 0m is preserved as Some and not confused with None.
Test Class: FactoryAndTemporalTests
5. FactorySuccess_ValidInput_CreatesValidatedApplication -> Asserts successful validated construction.
6. FactoryRejection_InvalidRules_RejectWithSpecificReason -> Asserts factory rejects invalid inputs across all boundary conditions.
7. RepeatedOperation_OnFinalizedState_IsSafelyRejected -> Asserts calling ApplyDecision on an Approved state returns InvalidStateTransition.

Test Class: PipelineWorkflowTests
8. CompleteSuccess_IntegratedResultChain_ProducesApprovedReceipt -> Complete end-to-end Result pipeline execution.
9. DomainFailure1_ScoreBelowThreshold_RejectsBeforeEvaluation -> Domain rejection when score is under 500.
10. DomainFailure2_ApplicantNotFound_RejectsWorkflow -> Domain rejection when registry lookup fails.
11. BoundaryCreditScore_AtEligibilityThreshold_PassesEligibility -> Verifies exact score limit of 500 passes.

Test Class: BoundaryIoTests
12. MissingFile_ReturnsFileNotFoundResultWithoutThrowing -> Boundary handles missing JSON file.
13. MalformedJsonFile_ReturnsMalformedFileResult -> Boundary handles corrupted JSON syntax.
14. ValidJsonFile_LoadsRecordsSuccessfully -> Boundary successfully reads and deserializes records.

Latest test run outcome: 14 passed, 0 failed.

7. TEAM RESPONSIBILITIES
Yernazar:

Implemented monadic primitives: Option<T> and Result<T, E> with Map and Bind.
Built ValidatedApplication factory with private constructor and invariant guards.
Implemented ApplicantRegistry supporting Option lookups and legitimate zero debt.
Authored FunctionalCoreTests.cs and FactoryAndTemporalTests.cs.
Dias:

Designed Discriminated Unions: DomainError and LoanWorkflowState.
Implemented ApplicationDataLoader with boundary exception handling for JSON.
Built LoanApplicationWorkflow composing Map/Bind steps into a single pipeline.
Authored PipelineWorkflowTests.cs, BoundaryIoTests.cs, and Program.cs demo.