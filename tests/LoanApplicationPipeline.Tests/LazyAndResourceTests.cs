using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Data;
using LoanApplicationPipeline.Domain;
using Xunit;

namespace LoanApplicationPipeline.Tests;

public class LazyAndResourceTests
{
    [Fact]
    public void Stream_DoesNothingUntilEnumerated_ThenProcessesDuringIteration()
    {
        var opens = 0;
        var stream = ApplicationFile.Stream(() => { opens++; return new StringReader(Samples.Csv()); });

        Assert.Equal(0, opens);                       // creating the enumerable ran no body code

        using var enumerator = stream.GetEnumerator();
        Assert.True(enumerator.MoveNext());           // iteration triggers processing
        Assert.Equal(1, opens);
        Assert.Equal("A001", Check.Ok(enumerator.Current).Id);
    }

    [Fact]
    public void Materialisation_GivesStableSnapshot_WhileRepeatedLazyEnumerationRerunsTheBody()
    {
        var opens = 0;
        var stream = ApplicationFile.Stream(() => { opens++; return new StringReader(Samples.Csv()); });

        var snapshot = stream.ToArray();              // deliberate materialisation point
        Assert.Equal(1, opens);
        Assert.Equal(10, snapshot.Length);

        _ = snapshot.Count();                         // reuse of the snapshot: no new reads
        _ = snapshot.ToList();
        Assert.Equal(1, opens);

        _ = stream.Count();                           // BEFORE materialisation each pass re-reads the source
        _ = stream.Count();
        Assert.Equal(3, opens);
    }

    [Fact]
    public void Stream_DisposesReader_AfterFullEnumeration()
    {
        var reader = new TrackingReader(Samples.Csv());
        var all = ApplicationFile.Stream(() => reader).ToArray();

        Assert.Equal(10, all.Length);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Stream_DisposesReader_WhenConsumerStopsEarly()
    {
        var reader = new TrackingReader(Samples.Csv());
        var firstTwo = ApplicationFile.Stream(() => reader).Take(2).ToArray();

        Assert.Equal(2, firstTwo.Length);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Stream_DisposesReader_WhenConsumerThrows()
    {
        var reader = new TrackingReader(Samples.Csv());

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

        Assert.Equal("file.unreadable", Check.FailCode(result));
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Load_FromFilePath_ReleasesFileHandle_Observable()
    {
        using var temp = new TempDir();
        var path = temp.File("apps.csv");
        File.WriteAllText(path, Samples.Csv());

        var loaded = Check.Ok(ApplicationFile.Load(path));

        Assert.Equal(10, loaded.Valid.Count);
        File.Delete(path);                            // would throw on Windows if the handle were still open
        Assert.False(File.Exists(path));
    }
}
