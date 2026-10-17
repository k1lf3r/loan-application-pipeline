LOAN APPLICATION PIPELINE - ASSIGNMENT 6: HANDLING TEMPORAL COUPLING

Team: Yernazar, Dias (confirm names and groups before submitting)
Course: Introduction to Functional Programming, 2026-2027
This project continues the Assignment 5 loan application pipeline (same repository, same domain).


1. PROJECT OVERVIEW

Workflow: raw application records -> validated factory -> explicit workflow states -> approval policy -> summary.

Input: RawApplicationRecord (ApplicantId, MonthlyIncome, RequestedAmount, CreditScore, HasExistingDebt as text).

Domain rules:
- ApplicantId required
- MonthlyIncome >= 0
- RequestedAmount > 0
- CreditScore in [300, 850]
- HasExistingDebt parses as true/false
- Eligibility threshold: CreditScore >= 500 (ApplicationTransitions.EligibilityThreshold)
- Conservative policy: min score 650, limit = income x 3; Flexible: min score 550, limit = income x 5;
  existing debt halves the limit; approved amount = min(requested, limit)

Output: ApplicationSummary (policy, submitted, eligible, approved, total approved amount, ordered decisions).


2. TASK 1 - HIDDEN DEPENDENCY (UNSAFE "BEFORE" EXAMPLE, documentation only)

In Assignment 5 the pipeline relied on two hidden steps that nothing enforced:
(1) the record must be validated by ApplicationParser.TryParse first;
(2) SelectEligibleForReview must run before ComputeDecisions.
ApplicationRequest had a public constructor and ComputeDecisions accepted any ApplicationRequest.

    // BEFORE (Assignment 5) - not part of the active code
    var bad = new ApplicationRequest("X", -5000m, 100m, 700, false);   // never validated
    var d = ApplicationStages.ComputeDecisions(new[] { bad },
                ApprovalPolicies.Conservative, "Conservative");
    // Approved = true, ApprovedAmount = -15000   (Math.Min(100, -5000 * 3))
    // Also: skipping SelectEligibleForReview lets a score-499 request reach the policy.

Prerequisite: validation, then eligibility review. Wrong order or a skipped step produced an approved
application with a negative amount, or an application that was never eligibility-checked.

AFTER (this version): the prerequisites are visible in the types.
    var outcome = ValidatedApplication.Create(raw);                  // only way to get a validated value
    var pending = ApplicationTransitions.Submit(app);                 // needs a ValidatedApplication
    var r1 = ApplicationTransitions.Decide(pending, policy);          // NotAllowed: review not started
    var r2 = ApplicationTransitions.StartReview(pending);             // Moved(UnderReview | Ineligible)
Calls that no longer compile (not included in the project):
    new ValidatedApplication(...)                  // constructor is private
    app with { CreditScore = 900 }                 // properties are get-only
    ApplicationWorkflow.Run(rawRecords, ...)       // needs ValidatedApplication, not raw data


3. TASK 2 - FACTORY

File: src/LoanApplicationPipeline/Models/ValidatedApplication.cs -> ValidatedApplication.Create
- Checks all five rules above (more than the required two) and reports every failed rule.
- Access restriction: private constructor + get-only properties. External code cannot construct an
  unchecked value or change one afterwards. Amounts are normalised to 2 decimals inside the factory.
- Returns ValidationOutcome.Valid (usable value) or ValidationOutcome.Invalid (reasons); no partial value escapes.
- Used by the real application: ApplicationIntake.Process (called from Program.cs) feeds ApplicationWorkflow.Run.


4. TASK 3 - DISTINCT CASES

ValidationOutcome (Models/ValidationOutcome.cs): Valid(ValidatedApplication) | Invalid(Reasons).
ApplicationState (Models/ApplicationState.cs), closed hierarchy (private base constructor):
- Pending(Application)               submitted, review not started
- UnderReview(Application)           score >= 500, waiting for a policy decision
- Approved(Application, Amount)      policy approved, carries the amount
- Declined(Application, Reason)      policy rejected, carries the reason
- Ineligible(Application, Threshold) score below threshold, never scored
TransitionOutcome (Models/TransitionOutcome.cs): Moved(Next) | NotAllowed(Reason).


5. TASK 4 - PATTERN MATCHING IN THE REAL PATH

- ApplicationIntake.Process: switch over ValidationOutcome (Valid -> accepted, Invalid -> reasons).
- ApplicationTransitions.StartReview / Decide: switch expressions over ApplicationState.
- ApplicationWorkflow.Process: switch over TransitionOutcome.
- ApplicationStages.BuildSummary: switch over every state case (Pending/UnderReview are rejected as non-final).
Fallbacks throw InvalidOperationException; no casts and no null-forgiving operators are used.


6. TASK 5 - SAFE OPERATIONS

- Transitions are pure: they take a state and return a new outcome; nothing is mutated.
- Decide accepts only an ApplicationState; the prerequisite (UnderReview) is visible and enforced.
- ApplyPolicy validates the policy result (negative amount, rejected-with-amount) before creating a new state.
- Failed transitions return NotAllowed; the original state, application and input collections are untouched.
- Repeated operation rule (documented): StartReview/Decide on a state that already moved on returns the
  EXISTING state unchanged (idempotent, same object).
- Demonstrations (see Program.cs output): one successful operation (A1), invalid cases (A6, A7, A8, A9 rejected by
  the factory; Decide on Pending rejected as an invalid sequence), repeated Decide.


7. HOW TO RUN

SDK: .NET 10 (check with "dotnet --list-sdks"; otherwise change <TargetFramework> in both .csproj files).
Working directory: repository root (where LoanApplicationPipeline.sln is).

    dotnet build
    dotnet run --project src/LoanApplicationPipeline
    dotnet test

Normal demonstration: run the console app - intake output, both policies, then the safe transition demo.
Failure demonstration: SKIP lines for A6-A9 and the "Rejected: Cannot decide..." line of the transition demo.
Prerequisites: .NET 10 SDK only; no network services, secrets or databases.


8. REQUIREMENT-TO-CODE MAP

Hidden dependency example ........ README.md section 2
Factory (Task 2) ................. Models/ValidatedApplication.cs -> Create
Distinct cases (Task 3) .......... Models/ValidationOutcome.cs, ApplicationState.cs, TransitionOutcome.cs
Pattern matching (Task 4) ........ Pipeline/ApplicationIntake.cs, ApplicationTransitions.cs, ApplicationWorkflow.cs, ApplicationStages.cs
Safe operations (Task 5) ......... Pipeline/ApplicationTransitions.cs (StartReview, Decide)
Workflow ......................... Pipeline/ApplicationWorkflow.cs -> Run, Process
Policies (from Assignment 5) ..... Pipeline/ApprovalPolicies.cs (now take ValidatedApplication)


9. TESTS (23 test methods; the factory theory has 9 data rows)

FactoryTests.cs
- Create_ValidInput_ReturnsValidWithExpectedData ........ FACTORY SUCCESS
- Create_InvalidInput_ReturnsInvalidWithReason (9 rows) . FACTORY REJECTION
- Create_CreditScoreBoundaries_FollowRange .............. BOUNDARY VALUE (300/299, 850/851)
- Create_MultipleBrokenFields_ReportsAllReasons
- Create_NormalisesAmountsToTwoDecimals
- Create_FailedConstruction_LeavesRawRecordUnchanged .... STATE PRESERVATION
TransitionTests.cs
- StartReview_ScoreAtThreshold_MovesToUnderReview ....... BOUNDARY VALUE (500)
- StartReview_ScoreBelowThreshold_MovesToIneligible ..... BOUNDARY VALUE (499), DISTINCT CASES
- FullFlow_ValidApplication_EndsApprovedWithExpectedAmount NORMAL OPERATION
- Decide_PolicyRejection_MovesToDeclined ................ DISTINCT CASES
- Decide_OnPending_IsNotAllowed_AndKeepsStateUnchanged .. MISSING PREREQUISITE, STATE PRESERVATION
- Decide_PolicyReturnsNegativeAmount_IsNotAllowed ....... validation before commit
- RepeatedOperations_ReturnExistingState_WithoutChange .. REPEATED OPERATION
WorkflowTests.cs (Assignment 5 tests adapted + new)
- NormalWorkflow_BothPolicies_ApproveFullRequestedAmount
- EmptyDataset_ProducesEmptySummaryWithoutCrashing
- BoundaryCreditScore_AtEligibilityThreshold_IsIncludedInReview
- BoundaryCreditScore_JustBelowEligibilityThreshold_IsExcludedFromReview
- TwoPolicies_OnSameInput_ProduceDifferentApprovalDecision
- DebtSurcharge_CapsApprovedAmountDifferentlyPerPolicy
- Run_DoesNotMutateInput_AndIsRepeatable ................ STATE PRESERVATION
- BuildSummary_HandlesEveryFinalCase .................... DISTINCT CASES
- BuildSummary_RejectsNonFinalStates .................... MISSING PREREQUISITE
- Intake_SplitsValidAndInvalidRecords_AndKeepsInputUnchanged  DISTINCT CASES (Valid/Invalid)

Latest test run: <run "dotnet test" and paste the result and date here>


10. TEAM CONTRIBUTIONS (edit to match who really did what)

Yernazar: ValidatedApplication factory, ValidationOutcome, ApplicationIntake, FactoryTests.cs, README sections 2-3.
Dias: ApplicationState, TransitionOutcome, ApplicationTransitions, ApplicationWorkflow, ApplicationStages,
      TransitionTests.cs, WorkflowTests.cs, Program.cs demo, README sections 4-6.
Each member can explain and defend the whole workflow.
