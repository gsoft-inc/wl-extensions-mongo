using GSoft.ComponentModel.DataAnnotations;

namespace Workleap.Extensions.Mongo.Security;

public interface IMongoValueEncryptor
{
    byte[] Encrypt(byte[] bytes, SensitivityScope sensitivityScope);

    byte[] Decrypt(byte[] bytes, SensitivityScope sensitivityScope);
}