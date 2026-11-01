using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class BoundaryIoTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public BoundaryIoTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void MissingFile_ReturnsFileNotFoundResultWithoutThrowing()
    {
        string missingPath = Path.Combine(_tempDir, "missing.json");

        var result = ApplicationDataLoader.LoadFromJsonFile(missingPath);

        Assert.True(result.IsFailure);
        var err = ((Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Err)result).Error;
        var fileErr = Assert.IsType<DomainError.FileNotFound>(err);
        Assert.Equal(missingPath, fileErr.FilePath);
    }

    [Fact]
    public void MalformedJsonFile_ReturnsMalformedFileResult()
    {
        string malformedPath = Path.Combine(_tempDir, "broken.json");
        File.WriteAllText(malformedPath, "{ invalid json structure");

        var result = ApplicationDataLoader.LoadFromJsonFile(malformedPath);

        Assert.True(result.IsFailure);
        var err = ((Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Err)result).Error;
        var malformedErr = Assert.IsType<DomainError.MalformedFile>(err);
        Assert.Equal(malformedPath, malformedErr.FilePath);
    }

    [Fact]
    public void ValidJsonFile_LoadsRecordsSuccessfully()
    {
        string validPath = Path.Combine(_tempDir, "valid.json");
        File.WriteAllText(validPath, """[{"ApplicantId":"A1","MonthlyIncomeText":"50000","RequestedAmountText":"100000","CreditScoreText":"700","HasExistingDebtText":"false"}]""");

        var result = ApplicationDataLoader.LoadFromJsonFile(validPath);

        Assert.True(result.IsSuccess);
        var records = ((Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Ok)result).Value;
        Assert.Single(records);
        Assert.Equal("A1", records[0].ApplicantId);
    }
}