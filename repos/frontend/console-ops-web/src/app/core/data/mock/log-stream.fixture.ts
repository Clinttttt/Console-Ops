import { LogEvent, LogMarker, LogStream, LogStreamItem } from '../../contracts/log-stream';

/**
 * Design-stage fixture for the Logs screen.
 *
 * Not observed from anything: Console Ops has no log ingestion, so the screen is labelled as sample data
 * and this file exists to design against. It deliberately includes the awkward cases a real stream has -
 * an event with no outcome, one with no correlation ids, an exception with a stack trace, a warning that
 * precedes the error it explains, and markers that change what the following lines mean.
 */

const OBSERVED_AT = '2026-08-15T23:55:00.000Z';

function event(entry: Omit<LogEvent, 'kind'>): LogEvent {
  return { kind: 'event', ...entry };
}

function marker(entry: Omit<LogMarker, 'kind'>): LogMarker {
  return { kind: 'marker', ...entry };
}

/** Oldest first: the stream reads like a terminal, and a live tail would append at the bottom. */
const ITEMS: readonly LogStreamItem[] = [
  event({
    id: 'event-1',
    occurredAt: '2026-08-15T23:52:11.997Z',
    level: 'info',
    source: 'Runtime',
    sourceKind: 'runtime',
    message: 'Starting initialization',
    messageTemplate: null,
    outcome: null,
    correlation: { traceId: null, requestId: null },
    exception: null,
    properties: [],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-2',
    occurredAt: '2026-08-15T23:52:12.512Z',
    level: 'info',
    source: 'Runtime',
    sourceKind: 'runtime',
    message: 'Container initialized',
    messageTemplate: 'Container initialized from {Image}',
    outcome: null,
    correlation: { traceId: null, requestId: null },
    exception: null,
    properties: [{ name: 'Image', value: 'spinner-api:1.23.4' }],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-3',
    occurredAt: '2026-08-15T23:52:13.782Z',
    level: 'info',
    source: 'Database',
    sourceKind: 'application',
    message: 'Database connection established',
    messageTemplate: 'Database connection established to {Server}',
    outcome: { statusCode: null, durationMs: 12 },
    correlation: { traceId: null, requestId: null },
    exception: null,
    properties: [{ name: 'Server', value: 'PostgreSQL 15' }],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-4',
    occurredAt: '2026-08-15T23:52:14.011Z',
    level: 'info',
    source: 'Runtime',
    sourceKind: 'runtime',
    message: 'Application started',
    messageTemplate: 'Application started {Version} build {Build}',
    outcome: null,
    correlation: { traceId: null, requestId: null },
    exception: null,
    properties: [
      { name: 'Version', value: '1.23.4' },
      { name: 'Build', value: '1587' },
    ],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  marker({
    id: 'marker-revision',
    occurredAt: '2026-08-15T23:52:14.200Z',
    markerKind: 'revision',
    commitShortSha: '8a17c2f',
    revision: 'spinner-api--000021',
    deploymentId: null,
  }),
  event({
    id: 'event-5',
    occurredAt: '2026-08-15T23:52:47.531Z',
    level: 'info',
    source: 'Orders',
    sourceKind: 'application',
    message: 'Order created',
    messageTemplate: 'Order {OrderId} created for customer {CustomerId}',
    outcome: null,
    correlation: {
      traceId: '4f2b9c7e6d1a4c7bb8e2f9a1d3b6c8e0',
      requestId: 'req_01HX7V5P4C6Q7R2S8T9U1V0WY',
    },
    exception: null,
    properties: [
      { name: 'OrderId', value: '2047' },
      { name: 'CustomerId', value: '91' },
    ],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-6',
    occurredAt: '2026-08-15T23:53:12.781Z',
    level: 'error',
    source: 'Payments',
    sourceKind: 'application',
    message: 'Payment provider returned an error',
    messageTemplate: 'Payment provider returned {StatusCode} for order {OrderId}',
    outcome: { statusCode: 502, durationMs: 2814 },
    correlation: {
      traceId: '4f2b9c7e6d1a4c7bb8e2f9a1d3b6c8e0',
      requestId: 'req_01HX7V5P4C6Q7R2S8T9U1V0WZ',
    },
    exception: {
      type: 'HttpRequestException',
      message: 'Response status code does not indicate success: 502 (Bad Gateway).',
      stackTrace: [
        'System.Net.Http.HttpRequestException: Response status code does not indicate success: 502 (Bad Gateway).',
        '   at System.Net.Http.HttpResponseMessage.EnsureSuccessStatusCode()',
        '   at Spinner.Payments.ProviderClient.ChargeAsync(ChargeRequest request, CancellationToken cancellationToken)',
        '   at Spinner.Orders.CheckoutHandler.Handle(CheckoutCommand command, CancellationToken cancellationToken)',
      ].join('\n'),
    },
    properties: [
      { name: 'OrderId', value: '2048' },
      { name: 'Provider', value: 'Stripe' },
      { name: 'StatusCode', value: '502' },
    ],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-7',
    occurredAt: '2026-08-15T23:53:41.289Z',
    level: 'info',
    source: 'Payments',
    sourceKind: 'application',
    message: 'Charge authorized',
    messageTemplate: 'Charge authorized by {Provider}',
    outcome: { statusCode: 200, durationMs: 189 },
    correlation: {
      traceId: '4f2b9c7e6d1a4c7bb8e2f9a1d3b6c8e0',
      requestId: 'req_01HX7V5P4C6Q7R2S8T9U1V0X0',
    },
    exception: null,
    properties: [{ name: 'Provider', value: 'Stripe' }],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-8',
    occurredAt: '2026-08-15T23:53:58.612Z',
    level: 'warning',
    source: 'Payments',
    sourceKind: 'application',
    message: 'Provider request required a retry',
    messageTemplate: 'Provider request required a retry, attempt {Attempt}',
    outcome: { statusCode: 502, durationMs: 1842 },
    correlation: {
      traceId: '4f2b9c7e6d1a4c7bb8e2f9a1d3b6c8e0',
      requestId: 'req_01HX7V5P4C6Q7R2S8T9U1V0WZ',
    },
    exception: null,
    properties: [
      { name: 'OrderId', value: '2048' },
      { name: 'Attempt', value: '2' },
      { name: 'Provider', value: 'Stripe' },
      { name: 'StatusCode', value: '502' },
    ],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-9',
    occurredAt: '2026-08-15T23:54:22.104Z',
    level: 'info',
    source: 'Orders',
    sourceKind: 'application',
    message: 'Order completed',
    messageTemplate: 'Order {OrderId} completed for customer {CustomerId}',
    outcome: null,
    correlation: {
      traceId: '4f2b9c7e6d1a4c7bb8e2f9a1d3b6c8e0',
      requestId: 'req_01HX7V5P4C6Q7R2S8T9U1V0X1',
    },
    exception: null,
    properties: [
      { name: 'OrderId', value: '2048' },
      { name: 'CustomerId', value: '92' },
    ],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-10',
    occurredAt: '2026-08-15T23:54:31.940Z',
    level: 'info',
    source: 'HTTP',
    sourceKind: 'application',
    message: 'GET /health completed',
    messageTemplate: '{Method} {Path} completed',
    outcome: { statusCode: 200, durationMs: 7 },
    correlation: { traceId: null, requestId: 'req_01HX7V5P4C6Q7R2S8T9U1V0X2' },
    exception: null,
    properties: [
      { name: 'Method', value: 'GET' },
      { name: 'Path', value: '/health' },
    ],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  marker({
    id: 'marker-deployment',
    occurredAt: '2026-08-15T23:54:40.000Z',
    markerKind: 'deployment',
    commitShortSha: '9047c89',
    revision: null,
    deploymentId: 'deployment-9047c89',
  }),
  event({
    id: 'event-11',
    occurredAt: '2026-08-15T23:54:44.128Z',
    level: 'info',
    source: 'Runtime',
    sourceKind: 'runtime',
    message: 'Container restarting for a new revision',
    messageTemplate: null,
    outcome: null,
    correlation: { traceId: null, requestId: null },
    exception: null,
    properties: [],
    host: 'spinner-api-7d8c9f6b5c-xk2pz',
  }),
  event({
    id: 'event-12',
    occurredAt: '2026-08-15T23:54:51.006Z',
    level: 'error',
    source: 'Database',
    sourceKind: 'application',
    message: 'Connection attempt timed out',
    messageTemplate: 'Connection attempt timed out after {TimeoutMs} ms',
    outcome: { statusCode: null, durationMs: 5000 },
    correlation: { traceId: 'b71ce4d2a9f04c1e8a3d5f7091b2c4d6', requestId: null },
    exception: {
      type: 'NpgsqlException',
      message: 'The operation has timed out.',
      stackTrace: null,
    },
    properties: [
      { name: 'TimeoutMs', value: '5000' },
      { name: 'Server', value: 'PostgreSQL 15' },
    ],
    host: 'spinner-api-6f4b81c9d2-q7wme',
  }),
];

export const LOG_STREAM_FIXTURE: LogStream = {
  observedAt: OBSERVED_AT,
  scopes: [
    {
      projectId: 'project-spinner',
      projectName: 'Spinner API',
      environment: { id: 'env-production', name: 'Production', kind: 'production' },
    },
    {
      projectId: 'project-spinner',
      projectName: 'Spinner API',
      environment: { id: 'env-staging', name: 'Staging', kind: 'staging' },
    },
    {
      projectId: 'project-stalltrack',
      projectName: 'StallTrack',
      environment: { id: 'env-stalltrack-production', name: 'Production', kind: 'production' },
    },
  ],
  items: ITEMS,
};
