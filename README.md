LOAN APPLICATION PIPELINE — ASSIGNMENT 5


1. PROJECT OVERVIEW

Topic: Loan application scoring and approval pipeline.

Intended users: Credit officers who need to quickly determine whether
an application is approved and for what amount.

Planned team: 2 students (Yernazar, Dias).

Input fields:
- ApplicantId (string) — unique applicant identifier
- MonthlyIncome (decimal) — applicant's monthly income
- RequestedAmount (decimal) — amount of credit requested
- CreditScore (int) — credit score, standard FICO-like scale
- HasExistingDebt (bool) — whether the applicant already has debt

Business rules:

Validation — all fields are required:
- MonthlyIncome >= 0
- RequestedAmount > 0
- CreditScore in range [300, 850]
- HasExistingDebt must parse as true/false

Stage 1 — Eligibility (independent of policy):
Only applications with CreditScore >= 500 are admitted for review.
Lower scores are rejected before any scoring happens.

Stage 2 — Approval policies (configurable, interchangeable):

Conservative policy:
- Minimum CreditScore to approve: 650
- Credit limit: MonthlyIncome x 3
- If HasExistingDebt = true: limit x 0.5
- Approved amount: min(RequestedAmount, limit) if score passes, else 0

Flexible policy:
- Minimum CreditScore to approve: 550
- Credit limit: MonthlyIncome x 5
- If HasExistingDebt = true: limit x 0.5
- Approved amount: min(RequestedAmount, limit) if score passes, else 0

Stage 3 — Summary:
Aggregates all decisions, sorts approved applications by approved
amount in descending order.

Expected output: An ApplicationSummary containing: policy name,
total submitted, number eligible for review, number approved,
total approved amount, and the ordered list of individual decisions.


2. DATASET AND EXPECTED RESULTS (documented before running)

A1: Income=50000, Requested=100000, CreditScore=700, Debt=false
    Valid — normal case
    Conservative: approved 100000
    Flexible: approved 100000

A2: Income=30000, Requested=20000, CreditScore=500, Debt=false
    Valid — boundary (exactly eligibility threshold)
    Admitted to review; both policies reject (500 < 650 and < 550)

A3: Income=40000, Requested=30000, CreditScore=600, Debt=false
    Valid — policies differ
    Conservative: rejected (600 < 650)
    Flexible: approved 30000 (600 >= 550)

A4: Income=10000, Requested=20000, CreditScore=700, Debt=true
    Valid — with existing debt
    Conservative: limit 15000 -> approved 15000 (capped)
    Flexible: limit 25000 -> approved 20000 (full)

A5: Income=20000, Requested=5000, CreditScore=499, Debt=false
    Valid input, but excluded at Stage 1 (below threshold)
    Not scored at all

A6: Income=(missing), Requested=5000, CreditScore=600, Debt=false
    Invalid — missing income
    Rejected at parsing

A7: Income=10000, Requested=5000, CreditScore="abc", Debt=false
    Invalid — CreditScore not a number
    Rejected at parsing

A8: Income=10000, Requested=5000, CreditScore=900, Debt=false
    Invalid — CreditScore out of range [300,850]
    Rejected at parsing

This dataset covers: normal flow, exact boundary inclusion/exclusion,
policy divergence, debt surcharge, and three distinct invalid-input
cases.


3. HOW TO RUN

SDK version: .NET 10 (verify with "dotnet --list-sdks"; if different,
update <TargetFramework> in both .csproj files accordingly).

Working directory: repository root (where the .sln file is located).

Commands:
  dotnet build
  dotnet run --project src/LoanApplicationPipeline
  dotnet test

Normal demonstration:
Running the console app prints parsing results for all 8 records,
then runs both policies (Conservative, Flexible) on the valid subset
and prints a summary for each.

Failure demonstration:
Records A6, A7, A8 in the console output show SKIP lines with
explicit rejection reasons (missing field, malformed value,
out-of-range value).


4. PIPELINE STAGES (Task 3 documentation)

Stage 1 — ApplicationStages.SelectEligibleForReview
Input type: IReadOnlyList<ApplicationRequest>
Output type: IReadOnlyList<ApplicationRequest>
Rule: applications with CreditScore < 500 are excluded from review;
remaining amounts are rounded to 2 decimal places.
Example: CreditScore = 500 -> included; CreditScore = 499 -> excluded.

Stage 2 — ApplicationStages.ComputeDecisions
Input type: IReadOnlyList<ApplicationRequest>,
Func<ApplicationRequest, (bool Approved, decimal ApprovedAmount)>
Output type: IReadOnlyList<ApplicationDecision>
Rule: the decision is computed entirely by the supplied policy
delegate; the stage itself holds no thresholds or limits.
Example: Conservative policy, CreditScore=700, Income=50000,
Requested=100000, no debt -> Approved=true, ApprovedAmount=100000.

Stage 3 — ApplicationStages.BuildSummary
Input type: IReadOnlyList<ApplicationDecision>, policy name,
total submitted count
Output type: ApplicationSummary
Rule: aggregates decisions into counts/totals and orders approved
applications by approved amount descending.
Example: one decision with Approved=true, ApprovedAmount=700
-> Summary.Approved=1, Summary.TotalApprovedAmount=700.

Stages are composed in ApplicationWorkflow.Run, which is the single
visible application workflow connecting all three stages.


5. REQUIREMENT-TO-CODE MAP

Typed input/output models
  Files: Models/RawApplicationRecord.cs, ApplicationRequest.cs (Yernazar);
         ApplicationDecision.cs, ApplicationSummary.cs (Dias)
  Author: Yernazar / Dias

Boundary validation (TryParse style)
  File: Validation/ApplicationParser.cs
  Author: Yernazar

Stage 1 (selection/normalisation)
  File: Pipeline/ApplicationStages.cs -> SelectEligibleForReview
  Author: Yernazar

Stage 2 (domain value computation)
  File: Pipeline/ApplicationStages.cs -> ComputeDecisions
  Author: Dias

Stage 3 (result model)
  File: Pipeline/ApplicationStages.cs -> BuildSummary
  Author: Dias

Higher-order function (Task 4)
  File: Pipeline/ApplicationWorkflow.cs -> Run(..., Func<ApplicationRequest,
        (bool,decimal)> approvalPolicy, ...)
  Author: Dias

Two interchangeable policies
  File: Pipeline/ApprovalPolicies.cs -> Conservative, Flexible
  Author: Dias

Pure functional core (Task 5)
  File: All of ApplicationStages.cs: no Console/File/DateTime/Random,
        returns new arrays, uses record "with" instead of mutation
  Author: Yernazar & Dias

Parsing tests
  File: tests/LoanApplicationPipeline.Tests/ParsingTests.cs
  Author: Yernazar

Pipeline/policy tests
  File: tests/LoanApplicationPipeline.Tests/PipelineTests.cs
  Author: Dias


6. TEST COVERAGE

ValidRecord_IsParsedIntoDomainModel
  Covers: Baseline parsing correctness

InvalidAndMissingInput_AreRejectedAtParsing
  Covers: INVALID AND MISSING INPUT

SelectEligibleForReview_DoesNotMutateCallerOwnedInput
  Covers: PURITY AND STAGES

NormalWorkflow_BothPolicies_ApproveFullRequestedAmount
  Covers: NORMAL WORKFLOW

EmptyDataset_ProducesEmptySummaryWithoutCrashing
  Covers: EMPTY DATASET

BoundaryCreditScore_AtEligibilityThreshold_IsIncludedInReview
  Covers: BOUNDARY VALUE

BoundaryCreditScore_JustBelowEligibilityThreshold_IsExcludedFromReview
  Covers: BOUNDARY VALUE

TwoPolicies_OnSameInput_ProduceDifferentApprovalDecision
  Covers: TWO POLICIES

DebtSurcharge_CapsApprovedAmountDifferentlyPerPolicy
  Covers: TWO POLICIES (additional)

ComputeDecisions_DoesNotMutateCallerOwnedInput
  Covers: PURITY AND STAGES

Total: 9 automated tests (minimum required: 6).

Latest test run: dotnet test -> 9 passed, 0 failed, 13.10.2026


7. TEAM CONTRIBUTIONS

Yernazar:
Implemented RawApplicationRecord / ApplicationRequest models,
ApplicationParser (boundary validation), Stage 1
(SelectEligibleForReview), and ParsingTests.cs.

Dias:
Implemented ApplicationDecision / ApplicationSummary models,
ApprovalPolicies (two policies), Stage 2 and Stage 3,
ApplicationWorkflow, Program.cs, and PipelineTests.cs.

Both members reviewed and can defend the full pipeline end to end.


8. PREREQUISITES

- .NET 10 SDK (or adjust TargetFramework to match the installed SDK)
- No external services, databases, or network access required
- No secrets or API keys used in this project