import * as path from 'path';
import * as cdk from 'aws-cdk-lib';
import * as cloudfront from 'aws-cdk-lib/aws-cloudfront';
import * as origins from 'aws-cdk-lib/aws-cloudfront-origins';
import * as events from 'aws-cdk-lib/aws-events';
import * as targets from 'aws-cdk-lib/aws-events-targets';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as s3 from 'aws-cdk-lib/aws-s3';
import { Platform } from 'aws-cdk-lib/aws-ecr-assets';
import { Construct } from 'constructs';

export interface CosydaysStackProps extends cdk.StackProps {
  dbConnectionString: string;
  octopusAccountNumber?: string;
  octopusApiKey?: string;
  octopusEuid?: string;
  anthropicApiKey?: string;
}

export class CosydaysStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props: CosydaysStackProps) {
    super(scope, id, props);

    const repoRoot = path.join(__dirname, '..', '..', '..');

    // ── Shared Lambda environment ──────────────────────────────────────────
    const sharedEnv: Record<string, string> = {
      ConnectionStrings__cosydb: props.dbConnectionString,
      SKIP_AUTO_MIGRATE: 'true',
    };
    if (props.octopusAccountNumber) sharedEnv.OCTOPUS_ACCOUNT_NUMBER = props.octopusAccountNumber;
    if (props.octopusApiKey) sharedEnv.OCTOPUS_API_KEY = props.octopusApiKey;
    if (props.octopusEuid) sharedEnv.OCTOPUS_EUID = props.octopusEuid;
    if (props.anthropicApiKey) sharedEnv['Anthropic__ApiKey'] = props.anthropicApiKey;

    // ── API Lambda ─────────────────────────────────────────────────────────
    const apiFunction = new lambda.DockerImageFunction(this, 'ApiFunction', {
      functionName: 'cosydays-api',
      code: lambda.DockerImageCode.fromImageAsset(repoRoot, {
        file: 'OctopusCosyAnalyser.ApiService/Dockerfile',
        platform: Platform.LINUX_ARM64,
      }),
      architecture: lambda.Architecture.ARM_64,
      memorySize: 512,
      timeout: cdk.Duration.seconds(300),
      environment: sharedEnv,
    });

    const apiUrl = apiFunction.addFunctionUrl({
      authType: lambda.FunctionUrlAuthType.NONE,
    });

    // ── Worker Lambda ──────────────────────────────────────────────────────
    const workerFunction = new lambda.DockerImageFunction(this, 'WorkerFunction', {
      functionName: 'cosydays-worker',
      code: lambda.DockerImageCode.fromImageAsset(repoRoot, {
        file: 'OctopusCosyAnalyser.ApiService/Dockerfile',
        platform: Platform.LINUX_ARM64,
      }),
      architecture: lambda.Architecture.ARM_64,
      memorySize: 512,
      timeout: cdk.Duration.seconds(900),
      environment: {
        ...sharedEnv,
        LAMBDA_WORKER_MODE: 'true',
      },
    });

    // ── EventBridge schedules ──────────────────────────────────────────────
    const workers = [
      { name: 'snapshot', minutes: 30 },
      { name: 'timeseries', minutes: 30 },
      { name: 'cost', minutes: 360 },
      { name: 'energy-intervals', minutes: 35 },
    ];

    for (const w of workers) {
      new events.Rule(this, `Schedule-${w.name}`, {
        ruleName: `cosydays-${w.name}`,
        schedule: events.Schedule.rate(cdk.Duration.minutes(w.minutes)),
        targets: [
          new targets.LambdaFunction(workerFunction, {
            event: events.RuleTargetInput.fromObject({ worker: w.name }),
          }),
        ],
      });
    }

    // ── S3 bucket for PWA ──────────────────────────────────────────────────
    const webBucket = new s3.Bucket(this, 'WebBucket', {
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      removalPolicy: cdk.RemovalPolicy.DESTROY,
      autoDeleteObjects: true,
    });

    // ── CloudFront Function for SPA routing ────────────────────────────────
    // Rewrites non-file requests to /index.html for client-side routing.
    // Only attached to the S3 behavior — API 404s pass through untouched.
    const spaRouting = new cloudfront.Function(this, 'SpaRouting', {
      functionName: 'cosydays-spa-routing',
      code: cloudfront.FunctionCode.fromInline(
        'function handler(event) {\n'
        + '  var request = event.request;\n'
        + '  if (request.uri.includes(\'.\')) return request;\n'
        + '  request.uri = \'/index.html\';\n'
        + '  return request;\n'
        + '}'
      ),
      runtime: cloudfront.FunctionRuntime.JS_2_0,
    });

    // ── CloudFront distribution ────────────────────────────────────────────
    const distribution = new cloudfront.Distribution(this, 'Distribution', {
      defaultBehavior: {
        origin: origins.S3BucketOrigin.withOriginAccessControl(webBucket),
        viewerProtocolPolicy: cloudfront.ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
        cachePolicy: cloudfront.CachePolicy.CACHING_OPTIMIZED,
        functionAssociations: [{
          function: spaRouting,
          eventType: cloudfront.FunctionEventType.VIEWER_REQUEST,
        }],
      },
      additionalBehaviors: {
        '/api/*': {
          origin: new origins.FunctionUrlOrigin(apiUrl),
          viewerProtocolPolicy: cloudfront.ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
          cachePolicy: cloudfront.CachePolicy.CACHING_DISABLED,
          allowedMethods: cloudfront.AllowedMethods.ALLOW_ALL,
          originRequestPolicy: cloudfront.OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER,
        },
      },
      defaultRootObject: 'index.html',
    });

    // ── Outputs ────────────────────────────────────────────────────────────
    new cdk.CfnOutput(this, 'ApiUrl', { value: apiUrl.url });
    new cdk.CfnOutput(this, 'CloudFrontUrl', {
      value: `https://${distribution.distributionDomainName}`,
    });
    new cdk.CfnOutput(this, 'WebBucketName', { value: webBucket.bucketName });
    new cdk.CfnOutput(this, 'DistributionId', { value: distribution.distributionId });
    new cdk.CfnOutput(this, 'ApiFunctionName', { value: apiFunction.functionName });
    new cdk.CfnOutput(this, 'WorkerFunctionName', { value: workerFunction.functionName });
  }
}
