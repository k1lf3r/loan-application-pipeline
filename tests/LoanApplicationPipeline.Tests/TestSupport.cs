using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Data;
using LoanApplicationPipeline.Domain;
using Xunit.Sdk;

namespace LoanApplicationPipeline.Tests;

internal static class Check
{
    public static T Ok<T>(Result<T> result) =>
        result is Result<T>.Ok ok ? ok.Value : throw new XunitException($"Expected Ok but got {result}");

    public static string FailCode<T>(Result<T> result) =>
        result is Result<T>.Fail fail ? fail.Error.Code : throw new XunitException($"Expected Fail but got {result}");
}

internal static class Samples
{
    public static readonly string[] Rows =
    [
        "A001,Aigerim Bekova,Education,500000,12,1400,350000,720,0",
        "A002,Nurlan Sadykov,Consumer,1500000,24,1600,420000,680,40000",
        "A003,Dana Omarova,Housing,3000000,36,1200,600000,760,0",
        "A004,Timur Kasenov,Business,8000000,60,1500,900000,640,120000",
        "A005,Madina Akhmetova,Housing,12000000,120,1300,1100000,790,80000",
        "A006,Ruslan Ibragimov,Consumer,800000,18,1800,300000,610,60000",
        "A007,Saltanat Nurpeis,Education,2500000,48,1100,700000,740,0",
        "A008,Yerlan Dauletov,Business,6000000,84,1400,800000,700,150000",
        "A009,Aliya Tleuova,Consumer,300000,6,2000,250000,655,30000",
        "A010,Bauyrzhan Serik,Housing,20000000,180,1250,1500000,720,200000"
    ];

    public static string Csv(params string[] extraRows) =>
        string.Join("\n", new[] { ApplicationCsv.Header }.Concat(Rows).Concat(extraRows));

    /// <summary>Pure test data: payment is given directly, so no native library is involved.</summary>
    public static PricedApplication Priced(string id, LoanPurpose purpose, long amount, long payment, int credit,
                                           long income = 500_000, long debt = 0) =>
        new(Check.Ok(LoanApplication.Create(id, "Name " + id, purpose, amount, 12, 1200, income, credit, debt)), payment);

    public static PricedApplication[] Portfolio() =>
    [
        Priced("X4", LoanPurpose.Consumer, 500_000, 20_000, 640),
        Priced("X3", LoanPurpose.Education, 1_000_000, 85_000, 600),
        Priced("X2", LoanPurpose.Housing, 2_000_000, 40_000, 700),
        Priced("X1", LoanPurpose.Education, 1_000_000, 90_000, 600)
    ];

    public static int ByCredit(PricedApplication p) => p.Application.CreditScore;
}

internal sealed class TrackingReader(string text) : StringReader(text)
{
    public bool Disposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}

/// <summary>Returns the header, then throws IOException on the next read. Records disposal.</summary>
internal sealed class FailingReader : TextReader
{
    private int _calls;
    public bool Disposed { get; private set; }

    public override string? ReadLine()
    {
        _calls++;
        if (_calls == 1) return ApplicationCsv.Header;
        throw new IOException("simulated disk error");
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}

internal sealed class TempDir : IDisposable
{
    public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lp-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Root);

    public string File(string name) => System.IO.Path.Combine(Root, name);

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (Exception) { /* best effort */ }
    }
}
