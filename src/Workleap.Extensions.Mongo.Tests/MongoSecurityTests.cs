using GSoft.ComponentModel.DataAnnotations;
using GSoft.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Workleap.Extensions.Mongo.Tests;

public sealed class MongoSecurityTests : BaseIntegrationTest<MongoFixture>
{
    private const string FakeUserId1 = "22789b2f-9ae3-4753-a8e8-88d7867f0fce";
    private const string FakeUserId2 = "3da7d3e8-3402-4c2f-8fd8-e3832c9c3128";

    public MongoSecurityTests(MongoFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task Encryption_At_User_Scope_Prevents_One_User_From_Reading_Data_From_Another_User()
    {
        var database = this.Services.GetRequiredService<IMongoDatabase>();
        var collection = database.GetCollection<UserProtectionTestDocument>();
        var rawCollection = database.GetCollection<BsonDocument>(collection.GetName());
        var userContext = this.Services.GetRequiredService<AmbientUserContext>();

        var originalDoc = new UserProtectionTestDocument
        {
            UnprotectedValue = "unprotected",
            UserProtectedValue = "userProtected",
        };

        using (userContext.RegisterUserId(FakeUserId1))
        {
            await collection.InsertOneAsync(originalDoc);

            // Deserializing the document in a C# object decrypts the user-sensitive property
            var doc = await collection.Find(FilterDefinition<UserProtectionTestDocument>.Empty).SingleOrDefaultAsync();
            Assert.Equal(originalDoc.UnprotectedValue, doc.UnprotectedValue);
            Assert.Equal(originalDoc.UserProtectedValue, doc.UserProtectedValue);

            // Reading the raw BSON document does not decrypt the user-sensitive property serialized as binary data
            var rawDoc = await rawCollection.Find(FilterDefinition<BsonDocument>.Empty).SingleOrDefaultAsync();
            Assert.IsType<BsonString>(rawDoc["unprotectedValue"]);
            Assert.IsType<BsonBinaryData>(rawDoc["userProtectedValue"]);
        }

        // Cannot decrypt user-sensitive property without an ambient user ID
        await Assert.ThrowsAsync<FormatException>(async () =>
        {
            _ = await collection.Find(FilterDefinition<UserProtectionTestDocument>.Empty).SingleOrDefaultAsync();
        });

        // Cannot decrypt user-sensitive property with another ambient user ID
        await Assert.ThrowsAsync<FormatException>(async () =>
        {
            using (userContext.RegisterUserId(FakeUserId2))
            {
                _ = await collection.Find(FilterDefinition<UserProtectionTestDocument>.Empty).SingleOrDefaultAsync();
            }
        });

        // Decrypt user-sensitive property using the same user ID works
        using (userContext.RegisterUserId(FakeUserId1))
        {
            _ = await collection.Find(FilterDefinition<UserProtectionTestDocument>.Empty).SingleOrDefaultAsync();
        }
    }

    [Fact]
    public async Task Encryption_At_App_Scope_Works()
    {
        var database = this.Services.GetRequiredService<IMongoDatabase>();
        var collection = database.GetCollection<AppProtectionTestDocument>();
        var rawCollection = database.GetCollection<BsonDocument>(collection.GetName());
        var userContextFactory = this.Services.GetRequiredService<AmbientUserContext>();

        var originalDoc = new AppProtectionTestDocument
        {
            UnprotectedValue = "unprotected",
            AppProtectedValue = "appProtected",
        };

        // The current ambient user does not have any impact on a application-sensitive property
        using (userContextFactory.RegisterUserId(FakeUserId1))
        {
            await collection.InsertOneAsync(originalDoc);
        }

        // Deserializing the document in a C# object decrypts the application-sensitive property, even without a current ambient user ID
        var doc = await collection.Find(FilterDefinition<AppProtectionTestDocument>.Empty).SingleOrDefaultAsync();
        Assert.Equal(originalDoc.UnprotectedValue, doc.UnprotectedValue);
        Assert.Equal(originalDoc.AppProtectedValue, doc.AppProtectedValue);

        // Reading the raw BSON document does not decrypt the application-sensitive property serialized as binary data
        var rawDoc = await rawCollection.Find(FilterDefinition<BsonDocument>.Empty).SingleOrDefaultAsync();
        Assert.IsType<BsonString>(rawDoc["unprotectedValue"]);
        Assert.IsType<BsonBinaryData>(rawDoc["appProtectedValue"]);

        // Another ambient user ID does not have any impact on the decrypt process
        using (userContextFactory.RegisterUserId(FakeUserId2))
        {
            _ = await collection.Find(FilterDefinition<AppProtectionTestDocument>.Empty).SingleOrDefaultAsync();
        }
    }

    [MongoCollection("userProtectionTest")]
    private sealed class UserProtectionTestDocument : MongoDocument
    {
        [BsonElement("unprotectedValue")]
        public string UnprotectedValue { get; set; } = string.Empty;

        [BsonElement("userProtectedValue")]
        [SensitiveInformation(SensitivityScope.User)]
        public string UserProtectedValue { get; set; } = string.Empty;
    }

    [MongoCollection("appProtectionTest")]
    private sealed class AppProtectionTestDocument : MongoDocument
    {
        [BsonElement("unprotectedValue")]
        public string UnprotectedValue { get; set; } = string.Empty;

        [BsonElement("appProtectedValue")]
        [SensitiveInformation(SensitivityScope.Application)]
        public string AppProtectedValue { get; set; } = string.Empty;
    }
}