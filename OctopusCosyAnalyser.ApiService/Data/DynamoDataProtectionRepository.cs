using System.Xml.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace OctopusCosyAnalyser.ApiService.Data;

/// <summary>
/// Persists ASP.NET Core Data Protection keys to DynamoDB instead of the local filesystem,
/// so encryption keys survive Lambda cold starts and are shared between the API and worker
/// Lambda functions.
///
/// Keys live in the shared table under a fixed system partition:
/// PK = "SYSTEM", SK = "DPKEY#{friendlyName}".
/// </summary>
public sealed class DynamoDataProtectionRepository : IXmlRepository
{
    private const string PartitionKeyValue = "SYSTEM";
    private const string SortKeyPrefix = "DPKEY#";

    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public DynamoDataProtectionRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
        _tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ?? "cosydays";
    }

    /// <summary>
    /// Returns every stored Data Protection key as an <see cref="XElement"/>.
    /// <see cref="IXmlRepository"/> is a synchronous interface invoked only once during
    /// host startup (when the Data Protection key ring is loaded), so blocking on the
    /// async DynamoDB call here is acceptable — there is no request context to deadlock.
    /// </summary>
    public IReadOnlyCollection<XElement> GetAllElements() =>
        GetAllElementsAsync().GetAwaiter().GetResult();

    private async Task<IReadOnlyCollection<XElement>> GetAllElementsAsync()
    {
        var elements = new List<XElement>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = null;

        do
        {
            var response = await _dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new AttributeValue { S = PartitionKeyValue },
                    [":skPrefix"] = new AttributeValue { S = SortKeyPrefix },
                },
                ExclusiveStartKey = exclusiveStartKey,
            }).ConfigureAwait(false);

            foreach (var item in response.Items)
            {
                if (item.TryGetValue("Xml", out var xml) && !string.IsNullOrEmpty(xml.S))
                {
                    elements.Add(XElement.Parse(xml.S));
                }
            }

            // DynamoDB signals "no more pages" with an empty (not null) LastEvaluatedKey.
            exclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 }
                ? response.LastEvaluatedKey
                : null;
        } while (exclusiveStartKey is not null);

        return elements;
    }

    /// <summary>
    /// Stores (or overwrites) a single Data Protection key.
    /// See <see cref="GetAllElements"/> for why this blocks synchronously.
    /// </summary>
    public void StoreElement(XElement element, string friendlyName) =>
        StoreElementAsync(element, friendlyName).GetAwaiter().GetResult();

    private async Task StoreElementAsync(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(friendlyName);

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = PartitionKeyValue },
                ["SK"] = new AttributeValue { S = SortKeyPrefix + friendlyName },
                ["Xml"] = new AttributeValue { S = element.ToString(SaveOptions.DisableFormatting) },
            },
        }).ConfigureAwait(false);
    }
}

/// <summary>
/// Wires up DynamoDB-backed storage for ASP.NET Core Data Protection keys.
/// </summary>
public static class DataProtectionDynamoExtensions
{
    public static IDataProtectionBuilder PersistKeysToDynamoDb(this IDataProtectionBuilder builder)
    {
        builder.Services.AddSingleton<IXmlRepository, DynamoDataProtectionRepository>();
        return builder;
    }
}
