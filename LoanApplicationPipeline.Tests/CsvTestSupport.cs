using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

internal static class CsvSamples
{
    public static readonly string[] Rows =
    [
        "A001,P001,Education,500000,12,1400,350000,720",
        "A002,P002,Consumer,1500000,24,1600,420000,680",
        "A003,P003,Housing,3000000,36,1200,600000,760",
        "A004,P004,Business,8000000,60,1500,900000,640",
        "A005,P005,Housing,12000000,120,1300,1100000,790",
        "A006,P006,Consumer,800000,18,1800,300000,610",
        "A007,P007,Education,2500000,48,1100,700000,740",
        "A008,P008,Business,6000000,84,1400,800000,700",
        "A009,P009,Consumer,300000,6,2000,250000,655",
        "A010,P010,Housing,20000000,180,1250,1500000,720"
    ];

    public static string Text(params string[] extraRows) =>
        string.Join("\n", new[] { ApplicationCsv.Header }.Concat(Rows).Concat(extraRows));

    public static Func<TextReader> Source(string text) => () => new StringReader(text);

    public static ApplicantRegistry Registry() => Samples.Registry(
        Samples.Profile("P001", 0), Samples.Profile("P002", 40_000), Samples.Profile("P003", 0),
        Samples.Profile("P004", 120_000), Samples.Profile("P005", 80_000), Samples.Profile("P006", 60_000),
        Samples.Profile("P007", 0), Samples.Profile("P008", 150_000), Samples.Profile("P009", 30_000),
        Samples.Profile("P010", 200_000), Samples.Profile("P011", 0, active: false));
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
