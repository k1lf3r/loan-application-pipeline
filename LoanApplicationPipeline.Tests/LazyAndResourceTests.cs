using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class LazyAndResourceTests
{
    [Fact]
    public void Stream_DoesNothingUntilEnumerated_ThenProcessesDuringIteration()
    {
        var opens = 0;
        var stream = ApplicationFile.Stream(() => { opens++; return new StringReader(CsvSamples.Text()); });

        Assert.Equal(0, opens);

        using var enumerator = stream.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, opens);
        Assert.Equal("A001", Check.Ok(enumerator.Current).Raw.Id);
        Assert.Equal(2, Check.Ok(enumerator.Current).Line);
    }

    [Fact]
    public void Materialisation_GivesStableSnapshot_WhileRepeatedLazyEnumerationRerunsTheBody()
    {
        var opens = 0;
        var stream = ApplicationFile.Stream(() => { opens++; return new StringReader(CsvSamples.Text()); });

        var snapshot = stream.ToArray();
        Assert.Equal(1, opens);
        Assert.Equal(10, snapshot.Length);

        _ = snapshot.Count();
        _ = snapshot.ToList();
        Assert.Equal(1, opens);

        _ = stream.Count();
        _ = stream.Count();
        Assert.Equal(3, opens);
    }

    [Fact]
    public void Stream_DisposesReader_AfterFullEnumeration()
    {
        var reader = new TrackingReader(CsvSamples.Text());

        var all = ApplicationFile.Stream(() => reader).ToArray();

        Assert.Equal(10, all.Length);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Stream_DisposesReader_WhenConsumerStopsEarly()
    {
        var reader = new TrackingReader(CsvSamples.Text());

        var firstTwo = ApplicationFile.Stream(() => reader).Take(2).ToArray();

        Assert.Equal(2, firstTwo.Length);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Stream_DisposesReader_WhenConsumerThrows()
    {
        var reader = new TrackingReader(CsvSamples.Text());

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var _ in ApplicationFile.Stream(() => reader))
                throw new InvalidOperationException("consumer failed");
        });

        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Load_WhenReadingFailsMidway_ReturnsFailure_AndDisposesReader()
    {
        var reader = new FailingReader();

        var result = ApplicationFile.Load(() => reader);

        Assert.Equal("FILE_UNREADABLE", Check.Code(result));
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void LoadFile_ReleasesFileHandle_Observable()
    {
        using var temp = new TempDir();
        var path = temp.File("apps.csv");
        File.WriteAllText(path, CsvSamples.Text());

        var loaded = Check.Ok(ApplicationFile.LoadFile(path));

        Assert.Equal(10, loaded.Records.Count);
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void PriceLazily_RunsNothingUntilIterated_ThenOneRecordPerStep()
    {
        var loaded = Check.Ok(ApplicationFile.Load(CsvSamples.Source(CsvSamples.Text())));
        var calculator = new StubCalculator(50_000);

        var lazy = PortfolioWorkflow.PriceLazily(loaded.Records, CsvSamples.Registry(), calculator);
        Assert.Equal(0, calculator.Calls);

        using var enumerator = lazy.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, calculator.Calls);
        Assert.Equal("A001", Check.Ok(enumerator.Current).Loan.Id);

        var rest = lazy.ToArray();
        Assert.Equal(10, rest.Length);
        Assert.Equal(11, calculator.Calls);
    }

    [Fact]
    public void PreparedPortfolio_IsASnapshot_ReuseDoesNotPriceAgain()
    {
        var calculator = new StubCalculator(50_000);
        var portfolio = Check.Ok(PortfolioWorkflow.Prepare(CsvSamples.Source(CsvSamples.Text()), CsvSamples.Registry(), calculator));
        Assert.Equal(10, calculator.Calls);

        _ = PortfolioWorkflow.BuildReport(portfolio, Samples.ByCredit, 650);
        _ = PortfolioWorkflow.BuildReport(portfolio, Samples.ByCredit, 700);
        _ = portfolio.Priced.Count();

        Assert.Equal(10, calculator.Calls);
    }
}
