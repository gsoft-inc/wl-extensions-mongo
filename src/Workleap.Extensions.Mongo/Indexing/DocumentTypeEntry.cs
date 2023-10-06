namespace Workleap.Extensions.Mongo.Indexing;

internal sealed class DocumentTypeEntry
{
    public DocumentTypeEntry(Type documentType, Type indexProviderType)
    {
        this.DocumentType = documentType;
        this.IndexProviderType = indexProviderType;
    }

    public Type DocumentType { get; }

    public Type IndexProviderType { get; }
}