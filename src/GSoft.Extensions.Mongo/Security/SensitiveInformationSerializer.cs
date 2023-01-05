using ShareGate.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GSoft.Extensions.Mongo.Security;

internal sealed class SensitiveInformationSerializer<T> : SerializerBase<T>
{
    private readonly IBsonSerializer<T> _underlyingSerializer;
    private readonly IMongoValueEncryptor _mongoValueEncryptor;
    private readonly SensitivityScope _sensitivityScope;

    public SensitiveInformationSerializer(IBsonSerializer<T> underlyingSerializer, IMongoValueEncryptor mongoValueEncryptor, SensitivityScope sensitivityScope)
    {
        this._underlyingSerializer = underlyingSerializer;
        this._mongoValueEncryptor = mongoValueEncryptor;
        this._sensitivityScope = sensitivityScope;
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
    {
        var valueBytes = this.GetBytes(value);
        var encryptedValueBytes = this._mongoValueEncryptor.Encrypt(valueBytes, this._sensitivityScope);
        context.Writer.WriteBinaryData(new BsonBinaryData(encryptedValueBytes));
    }

    private byte[] GetBytes(T value)
    {
        using var stream = new MemoryStream();
        using var writer = new BsonBinaryWriter(stream);

        writer.WriteStartDocument();
        writer.WriteName("0");
        this._underlyingSerializer.Serialize(BsonSerializationContext.CreateRoot(writer), value);
        writer.WriteEndDocument();

        return stream.ToArray();
    }

    public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var encryptedValueBytes = context.Reader.ReadBytes();
        var valueBytes = this._mongoValueEncryptor.Decrypt(encryptedValueBytes, this._sensitivityScope);
        return this.GetValue(valueBytes);
    }

    private T GetValue(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BsonBinaryReader(stream);

        reader.ReadStartDocument();
        reader.ReadName();
        var value = this._underlyingSerializer.Deserialize(BsonDeserializationContext.CreateRoot(reader));
        reader.ReadEndDocument();

        return value;
    }
}