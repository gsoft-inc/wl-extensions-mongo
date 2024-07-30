using Workleap.Extensions.Xunit;
using MongoDB.Driver;
using Workleap.Extensions.Mongo.Indexing;

namespace Workleap.Extensions.Mongo.Tests;

public sealed class UniqueIndexNameTests : BaseUnitTest
{
    public UniqueIndexNameTests(EmptyUnitFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture, testOutputHelper)
    {
    }

    [Theory]
    [InlineData("yup")]
    [InlineData("fn_ln")]
    [InlineData("FN_LN")]
    [InlineData("4Fun")]
    public void UniqueIndex_Created_When_Valid_IndexName_Specified(string indexName)
    {
        var index = CreateIndex(indexName);
        var result = UniqueIndexName.TryCreate(index, out var uxIndexName);

        Assert.True(result);
        Assert.NotNull(uxIndexName);
        Assert.Equal(indexName, uxIndexName.Prefix);
    }

    [Theory]
    [InlineData("fn-ln")]
    [InlineData("FN$ln")]
    [InlineData("FN&ln")]
    [InlineData("@ge")]
    public void UniqueIndex_NotCreated_When_Invalid_IndexName_Specified(string indexName)
    {
        var index = CreateIndex(indexName);
        var result = UniqueIndexName.TryCreate(index, out var uxIndexName);

        Assert.False(result);
        Assert.Null(uxIndexName);
    }

    [Fact]
    public void UniqueIndex_ComputeDefaultName_When_IndexName_NotSpecified()
    {
        var index = new CreateIndexModel<SampleDocument>(
            Builders<SampleDocument>.IndexKeys.Ascending(x => x.SampleField),
            options: null);

        var result = UniqueIndexName.TryCreate(index, out var uxIndexName);

        Assert.True(result);
        Assert.Equal("SampleField_1", uxIndexName!.Prefix);
    }

    private static CreateIndexModel<SampleDocument> CreateIndex(string indexName)
    {
        return new CreateIndexModel<SampleDocument>(
            Builders<SampleDocument>.IndexKeys.Combine(
                Builders<SampleDocument>.IndexKeys.Ascending(x => x.SampleField)),
            new CreateIndexOptions { Name = indexName });
    }

    [MongoCollection("Sample")]
    private sealed class SampleDocument : MongoDocument
    {
        public string? SampleField { get; set; }
    }
}