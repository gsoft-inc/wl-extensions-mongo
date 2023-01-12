using GSoft.ComponentModel.DataAnnotations;

namespace GSoft.Extensions.Mongo.Security;

public interface IMongoValueEncryptor
{
    byte[] Encrypt(byte[] bytes, SensitivityScope sensitivityScope);

    byte[] Decrypt(byte[] bytes, SensitivityScope sensitivityScope);
}