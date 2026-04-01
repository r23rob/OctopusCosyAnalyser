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

    /// <summary>
    /// Gets detailed field info including args and nested return type for a specific field on a type.
    /// </summary>
    public static string GetFieldDetails(string typeName, string fieldName)
    {
        return $$"""
        {
            "query": "{ __type(name: \"{{typeName}}\") { fields { name args { name type { name kind ofType { name kind ofType { name kind } } } } type { name kind ofType { name kind ofType { name kind } } fields { name type { name kind ofType { name kind ofType { name kind } } } } } } } }"
        }
        """;
    }

    /// <summary>
    /// Gets full type details including all fields, their types, and args — useful for exploring return types.
    /// </summary>
    public static string GetTypeDetails(string typeName)
    {
        return $$"""
        {
            "query": "{ __type(name: \"{{typeName}}\") { name kind fields { name args { name type { name kind ofType { name kind ofType { name kind } } } defaultValue } type { name kind ofType { name kind ofType { name kind } } } } } }"
        }
        """;
    }
}
