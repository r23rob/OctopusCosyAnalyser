#!/usr/bin/env node
import * as cdk from 'aws-cdk-lib';
import { CosydaysStack } from '../lib/cosydays-stack';

const app = new cdk.App();

const dbConnectionString = process.env.NEON_CONNECTION_STRING;
if (!dbConnectionString) {
  throw new Error(
    'NEON_CONNECTION_STRING environment variable is required.\n'
    + 'Set it to your Neon PostgreSQL connection string before deploying.'
  );
}

new CosydaysStack(app, 'CosydaysStack', {
  env: {
    region: process.env.AWS_REGION || 'eu-west-2',
    account: process.env.CDK_DEFAULT_ACCOUNT,
  },
  dbConnectionString,
  anthropicApiKey: process.env.ANTHROPIC_API_KEY,
});
