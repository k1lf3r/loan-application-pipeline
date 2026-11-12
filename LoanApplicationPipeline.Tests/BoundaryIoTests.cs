using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class BoundaryIoTests
{
    private const string TwoApplicants = """
        [
          { "applicantId": "P001", "fullName": "Aigerim", "existingDebtMonthly": 0,     "isActive": true },
          { "applicantId": "P002", "fullName": "Nurlan",  "existingDebtMonthly": 40000, "isActive": false }
        ]
        """;

    [Fact]
    public void MissingFile_ReturnsFileNotFoundResult_WithoutThrowing()
    {
        using var temp = new TempDir();
        var missing = temp.File("missing.json");

        var result = ApplicantFile.Load(missing);

        var error = Assert.IsType<DomainError.FileNotFound>(Check.Err(result));
        Assert.Equal(missing, error.FilePath);
        Assert.Equal("FILE_NOT_FOUND", error.Code);
    }

    [Fact]
    public void MissingFile_BlankPath_IsAlsoASafeFailure()
    {
        Assert.Equal("FILE_NOT_FOUND", Check.Code(ApplicantFile.Load("")));
    }

    [Fact]
    public void MalformedJsonFile_ReturnsMalformedFileResult()
    {
        using var temp = new TempDir();
        var path = temp.File("broken.json");
        File.WriteAllText(path, "{ invalid json structure");

        var result = ApplicantFile.Load(path);

        var error = Assert.IsType<DomainError.MalformedFile>(Check.Err(result));
        Assert.Equal(path, error.FilePath);
    }

    [Fact]
    public void ValidJsonFile_LoadsRegistry_AndLookupsWork()
    {
        using var temp = new TempDir();
        var path = temp.File("valid.json");
        File.WriteAllText(path, TwoApplicants);

        var registry = Check.Ok(ApplicantFile.Load(path));

        Assert.Equal(2, registry.Count);
        Assert.True(registry.FindById("P001").IsSome);
        Assert.True(registry.FindById("P003").IsNone);
        Assert.False(((Option<ApplicantProfile>.Some)registry.FindById("P002")).Value.IsActive);
    }

    [Fact]
    public void Parse_EmptyArray_IsAnEmptyButValidRegistry()
    {
        var registry = Check.Ok(ApplicantFile.Parse("[]", "inline"));

        Assert.Equal(0, registry.Count);
        Assert.True(registry.FindById("P001").IsNone);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{\"applicantId\":\"P1\"}")]
    [InlineData("[{\"applicantId\":\"P1\",\"fullName\":\"A\",\"existingDebtMonthly\":\"lots\",\"isActive\":true}]")]
    public void Parse_WrongShape_IsMalformedFile(string json)
    {
        Assert.IsType<DomainError.MalformedFile>(Check.Err(ApplicantFile.Parse(json, "inline")));
    }

    [Theory]
    [InlineData("[{\"fullName\":\"A\",\"existingDebtMonthly\":0,\"isActive\":true}]", "ApplicantId")]
    [InlineData("[{\"applicantId\":\"P1\",\"existingDebtMonthly\":0,\"isActive\":true}]", "FullName")]
    [InlineData("[{\"applicantId\":\"P1\",\"fullName\":\"A\",\"isActive\":true}]", "ExistingDebtMonthly")]
    [InlineData("[{\"applicantId\":\"P1\",\"fullName\":\"A\",\"existingDebtMonthly\":-1,\"isActive\":true}]", "ExistingDebtMonthly")]
    [InlineData("[{\"applicantId\":\"P1\",\"fullName\":\"A\",\"existingDebtMonthly\":0}]", "IsActive")]
    public void Parse_InvalidContent_IsRejectedWithRowAndField(string json, string expectedField)
    {
        var row = Assert.IsType<DomainError.RowRejected>(Check.Err(ApplicantFile.Parse(json, "inline")));

        Assert.Equal(1, row.Row);
        Assert.Equal(expectedField, Assert.IsType<DomainError.ValidationError>(row.Cause).Field);
    }

    [Fact]
    public void Parse_DuplicateApplicantIds_AreRejected()
    {
        const string json = """
            [
              { "applicantId": "P1", "fullName": "A", "existingDebtMonthly": 0, "isActive": true },
              { "applicantId": "P1", "fullName": "B", "existingDebtMonthly": 0, "isActive": true }
            ]
            """;

        var error = Assert.IsType<DomainError.ValidationError>(Check.Err(ApplicantFile.Parse(json, "inline")));
        Assert.Equal("ApplicantId", error.Field);
    }

    [Fact]
    public void SampleFile_ApplicantsJson_LoadsWithDocumentedContent()
    {
        var registry = Check.Ok(ApplicantFile.Load(RepoPaths.Data("applicants.json")));

        Assert.Equal(11, registry.Count);
        Assert.Equal(200_000, ((Option<ApplicantProfile>.Some)registry.FindById("P010")).Value.ExistingDebtMonthly);
        Assert.False(((Option<ApplicantProfile>.Some)registry.FindById("P011")).Value.IsActive);
    }
}
