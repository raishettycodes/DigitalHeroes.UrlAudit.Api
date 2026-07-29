# Digital Heroes URL Audit API

A simple ASP.NET Core Web API that audits website URLs and returns status code, response time, and reachability information.

## Endpoint

POST /api/audit

## Request

```json
{
  "url": "https://google.com"
}
```

## Response

```json
{
  "success": true,
  "url": "https://google.com",
  "statusCode": 200,
  "responseTimeMs": 120,
  "isReachable": true,
  "message": "URL audited successfully"
}
```