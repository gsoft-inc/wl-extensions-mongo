namespace Workleap.Extensions.Mongo.Indexing;

internal interface IIndexDetectionStrategy
{
    internal IReadOnlyList<Type> GetDocumentTypes(IEnumerable<Type> allTypes);
    
    void ValidateType(Type type);
    
    IndexRegistry CreateRegistry(IEnumerable<Type> documentTypes);
}