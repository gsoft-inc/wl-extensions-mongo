using GSoft.ComponentModel.DataAnnotations;

namespace GSoft.Infra.Mongo.Security;

internal sealed class NoopMongoValueEncryptor : IMongoValueEncryptor
{
    public byte[] Encrypt(byte[] bytes, SensitivityScope sensitivityScope) => bytes;

    public byte[] Decrypt(byte[] bytes, SensitivityScope sensitivityScope) => bytes;
}