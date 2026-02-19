namespace OctopusCosyAnalyser.ApiService.Services;

/// <summary>
/// Helper methods for GraphQL introspection queries
/// </summary>
public static class GraphQLIntrospection
{
    public static string GetTypeIntrospection(string typeName)
    {
        return $$"""
        {
            "query": "{ __type(name: \"{{typeName}}\") { fields { name type { name kind ofType { name kind } } } } }"
        }
        """;
    }

    public static string GetSchemaTypes()
    {
        return """
        {
            "query": "{ __schema { types { name kind } } }"
        }
        """;
    }
}
