using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Services;
using Xunit.Sdk;

namespace LoanApplicationPipeline.Tests;

internal static class Check
{
    public static T Ok<T>(Result<T, DomainError> result) =>
        result is Result<T, DomainError>.Ok ok ? ok.Value : throw new XunitException($"Expected Ok but got {result}");

    public static DomainError Err<T>(Result<T, DomainError> result) =>
        result is Result<T, DomainError>.Err err ? err.Error : throw new XunitException($"Expected Err but got {result}");

    public static string Code<T>(Result<T, DomainError> result) => Err(result).Code;
}

internal sealed class StubCalculator : IPaymentCalculator
{
    private readonly Func<long, int, int, Result<long, DomainError>> _implementation;

    public int Calls { get; private set; }

    public StubCalculator(long fixedPayment) : this((_, _, _) => Res.Ok(fixedPayment)) { }

    public StubCalculator(Func<long, int, int, Result<long, DomainError>> implementation) =>
        _implementation = implementation;

    public Result<long, DomainError> MonthlyPayment(long principalTenge, int annualRateBp, int termMonths)
    {
        Calls++;
        return _implementation(principalTenge, annualRateBp, termMonths);
    }
}

internal static class Samples
{
    public static ApplicantProfile Profile(string id = "P001", long debt = 0, bool active = true) =>
        new(id, "Name " + id, debt, active);

    public static ApplicantRegistry Registry(params ApplicantProfile[] profiles) =>
        Check.Ok(ApplicantRegistry.FromProfiles(profiles));

    public static RawApplicationRecord Raw(
        string id = "A1", string applicantId = "P001", string purpose = "Consumer", string amount = "1000000",
        string term = "12", string rate = "1200", string income = "400000", string credit = "700") =>
        new(id, applicantId, purpose, amount, term, rate, income, credit);

    public static ValidatedApplication Validated(
        string id = "A1", string applicantId = "P001", LoanPurpose purpose = LoanPurpose.Consumer,
        long amount = 1_000_000, int term = 12, int rate = 1200, long income = 400_000, int credit = 700) =>
        Check.Ok(ValidatedApplication.Create(Raw(id, applicantId, purpose.ToString(), amount.ToString(),
            term.ToString(), rate.ToString(), income.ToString(), credit.ToString())));

    public static PricedApplication Priced(string id, LoanPurpose purpose, long amount, long payment, int credit,
                                           long income = 500_000, long debt = 0) =>
        new(Validated(id, "P-" + id, purpose, amount, 12, 1200, income, credit), Profile("P-" + id, debt), payment);

    public static PricedApplication[] Portfolio() =>
    [
        Priced("X4", LoanPurpose.Consumer, 500_000, 20_000, 640),
        Priced("X3", LoanPurpose.Education, 1_000_000, 85_000, 600),
        Priced("X2", LoanPurpose.Housing, 2_000_000, 40_000, 700),
        Priced("X1", LoanPurpose.Education, 1_000_000, 90_000, 600)
    ];

    public static int ByCredit(PricedApplication p) => p.Loan.CreditScore;
}

internal sealed class TempDir : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "lp-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Root);

    public string File(string name) => Path.Combine(Root, name);

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (Exception) { /* best effort */ }
    }
}

internal static class RepoPaths
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(Path.Combine(dir.FullName, "LoanApplicationPipeline.slnx")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (LoanApplicationPipeline.slnx) not found.");
    }

    public static string Data(string fileName) =>
        Path.Combine(Root(), "LoanApplicationPipeline", "Data", fileName);
}
