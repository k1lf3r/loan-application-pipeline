using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Validation;
using Xunit;

namespace LoanApplicationPipeline.Tests;

public class ParsingTests
{
    [Fact]
    public void InvalidAndMissingInput_AreRejectedAtParsing()
    {
        var missingIncome = new RawApplicationRecord("A6", null, "5000", "600", "false");
        var malformedScore = new RawApplicationRecord("A7", "10000", "5000", "abc", "false");
        var outOfRangeScore = new RawApplicationRecord("A8", "10000", "5000", "900", "false");

        Assert.False(ApplicationParser.TryParse(missingIncome, out var r1, out var e1));
        Assert.Null(r1);
        Assert.Contains("отсутствует", e1);

        Assert.False(ApplicationParser.TryParse(malformedScore, out var r2, out var e2));
        Assert.Null(r2);
        Assert.Contains("не является", e2);

        Assert.False(ApplicationParser.TryParse(outOfRangeScore, out var r3, out var e3));
        Assert.Null(r3);
        Assert.Contains("диапазона", e3);
    }

    [Fact]
    public void ValidRecord_IsParsedIntoDomainModel()
    {
        var raw = new RawApplicationRecord("A1", "50000", "100000", "700", "false");

        Assert.True(ApplicationParser.TryParse(raw, out var request, out var error));
        Assert.Null(error);
        Assert.Equal(new ApplicationRequest("A1", 50000m, 100000m, 700, false), request);
    }

    [Fact]
    public void SelectEligibleForReview_DoesNotMutateCallerOwnedInput()
    {
        var original = new List<ApplicationRequest>
        {
            new("A1", 50000m, 100000m, 700, false),
            new("A5", 20000m, 5000m, 499, false),
        };
        var snapshot = original.ToArray();

        var eligible = ApplicationStages.SelectEligibleForReview(original);

        Assert.Equal(snapshot.Length, original.Count);
        for (int i = 0; i < original.Count; i++)
        {
            Assert.Equal(snapshot[i], original[i]);
        }

        Assert.NotSame(original, eligible);
        Assert.Single(eligible);
    }
}