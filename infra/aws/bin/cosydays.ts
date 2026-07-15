#!/usr/bin/env node
import * as cdk from 'aws-cdk-lib';
import { CosydaysStack } from '../lib/cosydays-stack';

const app = new cdk.App();

new CosydaysStack(app, 'CosydaysStack', {
  env: {
    region: process.env.AWS_REGION || 'eu-west-2',
    account: process.env.CDK_DEFAULT_ACCOUNT,
  },
  anthropicApiKey: process.env.ANTHROPIC_API_KEY,
});
