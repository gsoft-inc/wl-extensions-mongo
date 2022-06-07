using GSoft.Xunit.Extensions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace GSoft.Infra.Mongo.Tests;

public sealed class MongoBasicTests : BaseIntegrationTest<MongoFixture>
{
    public MongoBasicTests(MongoFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task Dates_Are_Always_Serialized_In_Utc()
    {
        var dtNow = DateTime.Now;
        var dtUtcNow = dtNow.ToUniversalTime();
        var dtoNow = DateTimeOffset.Now;
        var dtoUtcNow = dtoNow.ToUniversalTime();

        var brandsToInsert = new List<CarBrandDocument>
        {
            new CarBrandDocument { Name = "Toyota", DateTimeProperty = dtNow, DateTimeOffsetProperty = dtoNow },
            new CarBrandDocument { Name = "Honda", DateTimeProperty = dtUtcNow, DateTimeOffsetProperty = dtoUtcNow },
        };

        var collection = this.Services.GetRequiredService<IMongoDatabase>().GetCollection<CarBrandDocument>();
        await collection.InsertManyAsync(brandsToInsert);

        var insertedBrands = await collection.Find(FilterDefinition<CarBrandDocument>.Empty).ToAsyncEnumerable().ToListAsync();

        Assert.Equal(brandsToInsert.Count, insertedBrands.Count);
        var toyota = Assert.Single(insertedBrands, x => x.Name == "Toyota");
        var honda = Assert.Single(insertedBrands, x => x.Name == "Honda");

        Assert.Equal(dtUtcNow, toyota.DateTimeProperty);
        Assert.Equal(dtoUtcNow, toyota.DateTimeOffsetProperty);

        Assert.Equal(dtUtcNow, honda.DateTimeProperty);
        Assert.Equal(dtoUtcNow, honda.DateTimeOffsetProperty);
    }

    // BsonType.Int64 (ticks) and BsonType.Array (ticks, offset) are used to have the best precision
    // By default, we store dates as MongoDB datetimes which are precise to the millisecond
    [MongoCollection("carBrands")]
    private sealed class CarBrandDocument : MongoDocument
    {
        public string Name { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.Int64)]
        public new DateTime DateTimeProperty { get; set; }

        [BsonRepresentation(BsonType.Array)]
        public new DateTimeOffset DateTimeOffsetProperty { get; set; }
    }
}