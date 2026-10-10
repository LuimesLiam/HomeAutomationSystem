#!/usr/bin/env python3
"""Send synthetic OTLP data and verify trace storage, metric exemplars and log correlation."""
import json
import os
import secrets
import time
import urllib.parse
import urllib.request

collector = os.getenv('OTEL_HTTP_URL', 'http://127.0.0.1:4318').rstrip('/')
tempo = os.getenv('TEMPO_URL', 'http://127.0.0.1:3200').rstrip('/')
metrics = os.getenv('OTEL_METRICS_URL', 'http://127.0.0.1:8889').rstrip('/')
loki = os.getenv('LOKI_URL', 'http://127.0.0.1:3100').rstrip('/')
prometheus = os.getenv('PROMETHEUS_URL', '').rstrip('/')
trace_id, agent_id, model_id = secrets.token_hex(16), secrets.token_hex(8), secrets.token_hex(8)
now = time.time_ns()
resource = {'attributes': [{'key': 'service.name', 'value': {'stringValue': 'HomeApp.TelemetrySmoke'}}]}


def post(signal, payload):
    request = urllib.request.Request(f'{collector}/v1/{signal}', data=json.dumps(payload).encode(),
                                     headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(request, timeout=10) as response:
        assert response.status == 200
        result = json.load(response)
        assert not result.get('partialSuccess'), result


def get(url, accept='application/json'):
    request = urllib.request.Request(url, headers={'Accept': accept})
    with urllib.request.urlopen(request, timeout=10) as response:
        return response.read().decode()


def eventually(check):
    for attempt in range(30):
        try:
            if check():
                return
        except (urllib.error.HTTPError, urllib.error.URLError):
            pass
        time.sleep(1)
    raise AssertionError('Telemetry was not queryable within 30 seconds')


post('traces', {'resourceSpans': [{'resource': resource, 'scopeSpans': [
    {'scope': {'name': 'HomeApp.Agents'}, 'spans': [
        {'traceId': trace_id, 'spanId': agent_id, 'name': 'invoke_agent telemetry-smoke', 'kind': 1,
         'startTimeUnixNano': str(now), 'endTimeUnixNano': str(now + 100_000_000), 'status': {'code': 1}}]},
    {'scope': {'name': 'HomeApp.AI'}, 'spans': [
        {'traceId': trace_id, 'spanId': model_id, 'parentSpanId': agent_id, 'name': 'chat synthetic-model', 'kind': 3,
         'startTimeUnixNano': str(now), 'endTimeUnixNano': str(now + 50_000_000), 'status': {'code': 1},
         'attributes': [{'key': 'gen_ai.usage.input_tokens', 'value': {'intValue': '10'}}]}]}
]}]})
post('metrics', {'resourceMetrics': [{'resource': resource, 'scopeMetrics': [
    {'scope': {'name': 'HomeApp.AI'}, 'metrics': [
        {'name': 'homeapp.telemetry.smoke.duration', 'unit': 's', 'histogram': {'aggregationTemporality': 2,
         'dataPoints': [{'startTimeUnixNano': str(now), 'timeUnixNano': str(now + 100_000_000),
                         'count': '1', 'sum': .05, 'bucketCounts': ['1', '0'], 'explicitBounds': [.1],
                         'exemplars': [{'timeUnixNano': str(now), 'asDouble': .05,
                                        'traceId': trace_id, 'spanId': model_id}]}]}}]}
]}]})
post('logs', {'resourceLogs': [{'resource': resource, 'scopeLogs': [
    {'scope': {'name': 'HomeApp.TelemetrySmoke'}, 'logRecords': [
        {'timeUnixNano': str(now), 'observedTimeUnixNano': str(now), 'severityNumber': 9, 'severityText': 'INFO',
         'body': {'stringValue': 'Synthetic telemetry pipeline check'}, 'traceId': trace_id, 'spanId': model_id}]}
]}]})
eventually(lambda: 'invoke_agent telemetry-smoke' in get(f'{tempo}/api/traces/{trace_id}'))
eventually(lambda: trace_id in get(metrics + '/metrics', 'application/openmetrics-text; version=1.0.0'))
query = urllib.parse.quote('{service_name="HomeApp.TelemetrySmoke"} | trace_id="' + trace_id + '"')
eventually(lambda: 'Synthetic telemetry pipeline check' in get(loki + '/loki/api/v1/query_range?query=' + query))
if prometheus:
    expression = urllib.parse.quote('sum(traces_spanmetrics_calls_total{service="HomeApp.TelemetrySmoke"})')
    def span_metrics_present():
        result = json.loads(get(prometheus + '/api/v1/query?query=' + expression))['data']['result']
        return bool(result) and float(result[0]['value'][1]) > 0
    eventually(span_metrics_present)
print(f'PASS: correlated agent/model trace, metric exemplar and Loki log. Trace ID: {trace_id}')
