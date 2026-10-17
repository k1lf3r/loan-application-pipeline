using LoanApplicationPipeline.Models;
using Xunit;

namespace LoanApplicationPipeline.Tests;

internal static class TestData
{
    public static ValidatedApplication App(
        string id, string income, string amount, string score, string debt = "false")
    {
        var outcome = ValidatedApplication.Create(new RawApplicationRecord(id, income, amount, score, debt));
        return Assert.IsType<ValidationOutcome.Valid>(outcome).Application;
    }
}
