using ShareGate.ComponentModel.DataAnnotations;

namespace GSoft.Infra.Mongo.Security;

public interface IMongoValueEncryptor
{
    byte[] Encrypt(byte[] bytes, SensitivityScope sensitivityScope);

    byte[] Decrypt(byte[] bytes, SensitivityScope sensitivityScope);
}