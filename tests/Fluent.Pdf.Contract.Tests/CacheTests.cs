using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class CacheTests
{
    private static void Run(Action<PdfOperationContext> action, PdfOperationPolicy? policy = null, CancellationToken token = default) => PdfFont.Run("CacheFixture", "original.cache", policy, token, action);
    private static string Address(byte[] value) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompiledExamplesReturnDetachedTenantBytes(bool visualBasic)
    {
        using var cache = new PdfContentCache(); byte[] input = { 5,6,7 };
        Run(c =>
        {
            var result = visualBasic ? ContractExamples.VisualBasic.ContractExample.CacheInput(cache,input,c) : ContractExamples.CSharp.ContractExample.CacheInput(cache,input,c);
            Assert.Equal(input,result); Assert.NotSame(input,result); cache.Clear(); Assert.Equal(input,result);
        });
    }

    [Fact]
    public void AddressesAreComputedFromCapturedBytesAndCopiesCannotPoisonEntries()
    {
        using var cache = new PdfContentCache();
        byte[] input = { 1, 2, 3 }; string? address = null;
        Run(c => address = cache.Store(input, c));
        Assert.Equal(Address(input), address);
        input[0] = 99;
        Run(c => { Assert.True(cache.TryGet(address!, c, out var copy)); Assert.Equal(new byte[] { 1,2,3 }, copy); copy![1] = 99; });
        Run(c => { Assert.True(cache.TryGet(address!, c, out var copy)); Assert.Equal(new byte[] { 1,2,3 }, copy); Assert.Equal(address, cache.Store(copy!, c)); });
        Assert.Equal(1, cache.Count); Assert.Equal(3, cache.ResidentBytes);
    }
    [Fact]
    public void IdenticalAddressesDoNotGrantAccessToAnotherTenantInstance()
    {
        using var a = new PdfContentCache(); using var b = new PdfContentCache();
        string? address = null;
        Run(c => address = a.Store(new byte[] { 42 }, c));
        Run(c => { Assert.False(b.TryGet(address!,c,out var missing)); Assert.Null(missing); });
        a.Clear();
        Run(c => Assert.False(a.TryGet(address!,c,out _)));
    }
    [Fact]
    public void LruEvictsByBytesAndEntryCountAndReadsRefreshRecency()
    {
        using var cache = new PdfContentCache(2, 4, 2);
        string? a = null, b = null, d = null;
        Run(c => { a = cache.Store(new byte[] {1,1},c); b = cache.Store(new byte[] {2,2},c); Assert.True(cache.TryGet(a,c,out _)); d = cache.Store(new byte[] {3,3},c); });
        Run(c => { Assert.True(cache.TryGet(a!,c,out _)); Assert.False(cache.TryGet(b!,c,out _)); Assert.True(cache.TryGet(d!,c,out _)); });
        Assert.Equal(2,cache.Count); Assert.Equal(4,cache.ResidentBytes);
        Run(c => cache.Store(Array.Empty<byte>(),c));
        Assert.Equal(2,cache.Count); Assert.Equal(2,cache.ResidentBytes);
    }
    [Fact]
    public void EntryLimitIsLatchedWithoutChangingTheCache()
    {
        using var cache = new PdfContentCache(2, 4, 2);
        var error = Assert.Throws<PdfException>(() => Run(c =>
        {
            cache.Store(new byte[] {1,2},c);
            Assert.Throws<PdfException>(() => cache.Store(new byte[] {1,2,3},c));
            c.Checkpoint();
        }));
        Assert.Equal("Cache.EntryLimit",error.Diagnostic.Code);
        Assert.Equal(1,cache.Count); Assert.Equal(2,cache.ResidentBytes);
    }
    [Fact]
    public void CaptureAndHitRespectInputExpansionAndCancellationBeforeExposingData()
    {
        using var cache = new PdfContentCache(); byte[] input = {1,2,3};
        Assert.Equal("Limit.InputBytes",Assert.Throws<PdfException>(() => Run(c => cache.Store(input,c),new(limits:new(maximumInputBytes:2)))).Diagnostic.Code);
        Assert.Equal(0,cache.Count);
        Assert.Equal("Limit.ExpandedBytes",Assert.Throws<PdfException>(() => Run(c => cache.Store(input,c),new(limits:new(maximumExpandedBytes:2)))).Diagnostic.Code);
        string? address = null; Run(c => address=cache.Store(input,c));
        byte[]? output = null;
        Assert.Throws<PdfException>(() => Run(c => cache.TryGet(address!,c,out output),new(limits:new(maximumExpandedBytes:2))));
        Assert.Null(output);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Run(c => cache.Store(input,c),token:cancel.Token));
        Assert.Equal(1,cache.Count);
    }
    [Fact]
    public void DisposeClearsStorageAndCannotBeReopenedByAnAddress()
    {
        var cache = new PdfContentCache(); Run(c => cache.Store(new byte[] {9},c));
        cache.Dispose(); cache.Dispose();
        Assert.Equal(0,cache.Count); Assert.Equal(0,cache.ResidentBytes);
        Run(c => { Assert.Throws<ObjectDisposedException>(() => cache.Store(new byte[] {9},c)); Assert.Throws<ObjectDisposedException>(() => cache.TryGet(Address(new byte[] {9}),c,out _)); });
        Assert.Throws<ObjectDisposedException>(() => cache.Clear());
    }
    [Theory]
    [InlineData(0,4,2)] [InlineData(65537,4,2)] [InlineData(1,0,1)] [InlineData(1,4,5)] [InlineData(1,4,0)]
    public void InvalidCapacitiesReject(int entries,long bytes,int entry) => Assert.Throws<ArgumentOutOfRangeException>(()=>new PdfContentCache(entries,bytes,entry));
    [Theory]
    [InlineData("")] [InlineData("../tenant")] [InlineData("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    public void NoncanonicalAddressesReject(string address)
    { using var cache=new PdfContentCache(); Run(c=>Assert.Throws<ArgumentException>(()=>cache.TryGet(address,c,out _))); }
    [Fact]
    public async Task SixtyFourTenantsRunTwoHundredMixedCacheIterationsWithoutCrossReads()
    {
        using var pool = new PdfProcessingPool(new(64,64));
        var tasks = new List<Task<PdfOperationReport>>();
        for (int tenant=0;tenant<64;tenant++)
        {
            int identity=tenant;
            Assert.True(pool.TrySubmit(Array.Empty<byte>(),job=>
            {
                using var cache=new PdfContentCache(4,256,64);
                for(int iteration=0;iteration<200;iteration++)
                {
                    byte[] input={(byte)identity,(byte)iteration};
                    string address=cache.Store(input,job.Context);
                    Assert.True(cache.TryGet(address,job.Context,out var output)); Assert.Equal(input,output);
                    Assert.False(cache.TryGet(Address(new byte[]{(byte)((identity+1)%64),(byte)iteration}),job.Context,out _));
                    if(iteration%7==0)cache.Clear();
                    Assert.InRange(cache.ResidentBytes,0,256); Assert.InRange(cache.Count,0,4);
                }
            },out var task));
            tasks.Add(task!);
        }
        await Task.WhenAll(tasks); await pool.CompleteAsync();
    }
    [Fact]
    public void ConcurrentSharedInstanceNeverExceedsItsRetainedCapacity()
    {
        using var cache=new PdfContentCache(8,64,8);
        Parallel.For(0,32,worker=>Run(c=>
        {
            for(int i=0;i<100;i++)
            {
                string address=cache.Store(new byte[]{(byte)worker,(byte)i},c);
                if(cache.TryGet(address,c,out var copy))Assert.Equal(new byte[]{(byte)worker,(byte)i},copy);
                Assert.InRange(cache.Count,0,8); Assert.InRange(cache.ResidentBytes,0,64);
            }
        }));
    }
}
