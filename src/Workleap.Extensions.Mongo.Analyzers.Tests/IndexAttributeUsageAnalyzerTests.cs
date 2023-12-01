namespace Workleap.Extensions.Mongo.Analyzers.Tests;

public class IndexAttributeUsageAnalyzerTests : BaseAnalyzerTest<IndexAttributeUsageAnalyzer>
{
    private const string TestClassName = "MyWorker";
    
    private const string TestMethodName = "DoSomething";

    [Theory]
    [InlineData("[IndexedBy(\"PrimaryKey\")]")]
    [InlineData("[NoIndexNeeded(\"Default index used\")]")]
    public async Task Given_IndexAttribute_On_Class_When_Analyze_Then_No_Diagnostics(string attribute)
    {
        const string source = @"
public class PersonDocument : IMongoDocument {{ }}

{0}
public class MyWorker
{{
    public void DoSomething()
    {{
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.Find(FilterDefinition<PersonDocument>.Empty);
    }}
}}";

        await this.WithSourceCode(string.Format(source, attribute))
            .RunAsync();
    }
    
    [Theory]
    [InlineData("[IndexedBy(\"PrimaryKey\")]")]
    [InlineData("[NoIndexNeeded(\"Default index used\")]")]
    public async Task Given_IndexAttribute_On_Method_When_Analyze_Then_No_Diagnostics(string attribute)
    {
        const string source = @"
public class PersonDocument : IMongoDocument {{ }}

public class MyWorker
{{
    {0}
    public void DoSomething()
    {{
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.Find(FilterDefinition<PersonDocument>.Empty);
    }}
}}";

        await this.WithSourceCode(string.Format(source, attribute))
            .RunAsync();
    }

    [Fact]
    public async Task Given_No_IndexAttribute_When_Analyze_Then_Diagnostic()
    {
        const string source = @"
public class PersonDocument : IMongoDocument { }

public class MyWorker
{
    public void DoSomething()
    {
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.CountDocuments(FilterDefinition<PersonDocument>.Empty);
    }
}";

        await this.WithSourceCode(source)
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 9, startColumn: 13, endLine: 9, endColumn: 78, TestMethodName, TestClassName)
            .RunAsync();
    }
    
    [Theory]
    [InlineData("[IndexedBy(\"PrimaryKey\")]")]
    [InlineData("[NoIndexNeeded(\"Default index used\")]")]
    public async Task Given_No_IndexAttribute_On_Class_And_Missing_On_Some_Method_When_Analyze_Then_Diagnostic(string attribute)
    {
        const string source = @"
public class PersonDocument : IMongoDocument {{ }}

public class MyWorker
{{
    public void DoSomething()
    {{
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.CountDocuments(FilterDefinition<PersonDocument>.Empty);

        var collection2 = (IMongoCollection<PersonDocument>)null!;
        _ = collection2.CountDocuments(FilterDefinition<PersonDocument>.Empty);
    }}

    public void DoSomething2()
    {{
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.CountDocuments(FilterDefinition<PersonDocument>.Empty);

        var collection2 = (IMongoCollection<PersonDocument>)null!;
        _ = collection2.CountDocuments(FilterDefinition<PersonDocument>.Empty);
    }}

    {0}
    public void DoSomething3()
    {{
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.CountDocuments(FilterDefinition<PersonDocument>.Empty);
    }}
}}";

        await this.WithSourceCode(string.Format(source, attribute))
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 9, startColumn: 13, endLine: 9, endColumn: 78, TestMethodName, TestClassName)
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 18, startColumn: 13, endLine: 18, endColumn: 78, "DoSomething2", TestClassName)
            .RunAsync();
    }
    
    [Theory]
    [InlineData("[IndexedBy(\"PrimaryKey\")]")]
    [InlineData("[NoIndexNeeded(\"Default index used\")]")]
    public async Task Given_No_IndexAttribute_On_Not_All_Method_And_On_Class_When_Analyze_Then_No_Diagnostic(string attribute)
    {
        const string source = @"
public class PersonDocument : IMongoDocument {{ }}

{0}
public class MyWorker
{{
    public void DoSomething()
    {{
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.CountDocuments(FilterDefinition<PersonDocument>.Empty);
    }}

    {0}
    public void DoSomething2()
    {{
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.CountDocuments(FilterDefinition<PersonDocument>.Empty);
    }}
}}";

        await this.WithSourceCode(string.Format(source, attribute))
            .RunAsync();
    }

    [Fact]
    public async Task Given_No_IndexAttribute_And_Multiple_Mongo_Usage_When_Analyze_Then_Only_One_Diagnostic()
    {
        const string source = @"
public class PersonDocument : IMongoDocument { }

public class MyWorker
{
    public void DoSomething()
    {
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.Find(FilterDefinition<PersonDocument>.Empty);
        _ = collection.CountDocuments(FilterDefinition<PersonDocument>.Empty);
    }
}";

        await this.WithSourceCode(source)
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 9, startColumn: 13, endLine: 9, endColumn: 68, TestMethodName, TestClassName)
            .RunAsync();
    }

    [Fact]
    public async Task Given_No_IndexAttribute_And_Partial_Class_When_Analyze_Then_Diagnostic_Only_One_Class()
    {
        const string source = @"
public class PersonDocument : IMongoDocument { }

public partial class MyWorker
{
    public void DoSomething2()
    {
    }
}

public partial class MyWorker
{
    public void DoSomething()
    {
        var collection = (IMongoCollection<PersonDocument>)null!;
        _ = collection.Find(FilterDefinition<PersonDocument>.Empty);
    }
}";

        await this.WithSourceCode(source)
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 16, startColumn: 13, endLine: 16, endColumn: 68, TestMethodName, TestClassName)
            .RunAsync();
    }

    [Fact]
    public async Task Given_Two_Classes_In_Different_Namespace_When_Analyze_And_Only_One_Uses_Mongo_Then_Diagnostic_On_Only_One_Class()
    {
        const string source = @"
namespace FirstClass
{
    public class PersonDocument : IMongoDocument { }

    public class MyWorker
    {
        public void DoSomething()
        {
        }
    }
}

namespace SecondClass
{
    public class PersonDocument : IMongoDocument { }

    public class MyWorker
    {
        public void DoSomething()
        {
            var collection = (IMongoCollection<PersonDocument>)null!;
            _ = collection.Find(FilterDefinition<PersonDocument>.Empty);
        }
    }
}
";

        await this.WithSourceCode(source)
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 23, startColumn: 17, endLine: 23, endColumn: 72, TestMethodName, TestClassName)
            .RunAsync();
    }

    [Fact]
    public async Task Given_Two_Classes_In_Different_Namespace_When_Analyze_And_One_Uses_Attribute_Then_Diagnostic_On_Only_One_Class()
    {
        const string source = @"
namespace FirstClass
{
    public class PersonDocument : IMongoDocument { }

    [NoIndexNeeded(""Default index used"")]
    public class MyWorker
    {
        public void DoSomething()
        {
            var collection = (IMongoCollection<PersonDocument>)null!;
            _ = collection.Find(FilterDefinition<PersonDocument>.Empty);
        }
    }
}

namespace SecondClass
{
    public class PersonDocument : IMongoDocument { }

    public class MyWorker
    {
        public void DoSomething()
        {
            var collection = (IMongoCollection<PersonDocument>)null!;
            _ = collection.Find(FilterDefinition<PersonDocument>.Empty);
        }
    }
}
";

        await this.WithSourceCode(source)
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 26, startColumn: 17, endLine: 26, endColumn: 72, TestMethodName, TestClassName)
            .RunAsync();
    }

    [Fact]
    public async Task Given_Two_Classes_In_Different_Namespace_When_Analyze_Both_Miss_Attribute_Then_Diagnostic_Both_Classes()
    {
        const string source = @"
namespace FirstClass
{
    public class PersonDocument : IMongoDocument { }

    public class MyWorker
    {
        public void DoSomething()
        {
            var collection = (IMongoCollection<PersonDocument>)null!;
            _ = collection.CountDocuments(FilterDefinition<PersonDocument>.Empty);
        }
    }
}

namespace SecondClass
{
    public class PersonDocument : IMongoDocument { }

    public class MyWorker
    {
        public void DoSomething()
        {
            var collection = (IMongoCollection<PersonDocument>)null!;
            _ = collection.Find(FilterDefinition<PersonDocument>.Empty);
        }
    }
}
";

        await this.WithSourceCode(source)
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 11, startColumn: 17, endLine: 11, endColumn: 82, TestMethodName, TestClassName)
            .WithExpectedDiagnostic(IndexAttributeUsageAnalyzer.UseIndexAttributeRule, startLine: 25, startColumn: 17, endLine: 25, endColumn: 72, TestMethodName, TestClassName)
            .RunAsync();
    }

    [Fact]
    public async Task Given_Non_Mongo_Invocation_When_Analyze_Then_No_Diagnostic()
    {
        const string source = @"
public class PersonDocument : IMongoDocument { }

public class MyWorker
{
    public void DoSomething()
    {
        var number = int.Parse(""42"");
    }
}";

        await this.WithSourceCode(source)
            .RunAsync();
    }
}