namespace GSoft.Infra.Mongo.Security;

internal sealed class SensitiveInformationConventionPack : NamedConventionPack
{
    public SensitiveInformationConventionPack(IMongoValueEncryptor mongoValueEncryptor)
    {
        this.Add(new SensitiveInformationConvention(mongoValueEncryptor));
    }

    public override string Name => nameof(SensitiveInformationConventionPack);

    public override bool TypeFilter(Type type)
    {
        return SensitiveInformationConvention.IsSensitiveType(type);
    }
}