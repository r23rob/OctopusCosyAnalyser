using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

namespace OctopusCosyAnalyser.ApiService.Data;

public static class DynamoDbClientFactory
{
    // The default AWS region resolution chain (env vars, ~/.aws/config, Lambda runtime)
    // throws at construction when nothing is configured — e.g. CI runners and fresh dev
    // machines — which prevents the host from starting at all. Fall back to eu-west-2,
    // the CDK deploy default (infra/aws/bin/cosydays.ts). Instance metadata lookup is
    // skipped: Lambda always sets AWS_REGION, and probing IMDS off-EC2 just adds delay.
    public static AmazonDynamoDBClient Create() =>
        FallbackRegionFactory.GetRegionEndpoint(includeInstanceMetadata: false) is null
            ? new AmazonDynamoDBClient(RegionEndpoint.EUWest2)
            : new AmazonDynamoDBClient();
}
