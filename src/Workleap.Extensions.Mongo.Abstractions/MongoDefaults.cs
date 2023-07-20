namespace Workleap.Extensions.Mongo;

public static class MongoDefaults
{
    // We use named options to store multiple MongoClient configurations
    // This one is the option name for the default MongoClient
    // It's the same value than Microsoft.Extensions.Options.Options.DefaultName and it maps to IOptions<T> when a client name is not provided
    // https://docs.microsoft.com/en-us/dotnet/core/extensions/options
    // https://docs.microsoft.com/en-us/dotnet/core/extensions/options-library-authors
    public static readonly string ClientName = string.Empty;
}