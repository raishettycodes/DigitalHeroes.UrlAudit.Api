# DigitalHeroes.UrlAudit.Api

A production-ready ASP.NET Core 8 Web API that audits website URLs by checking availability, measuring response time, and returning HTTP status information. The project demonstrates clean architecture, dependency injection, middleware, caching, logging, configuration management, unit testing, and REST API best practices.

---

## Features

* URL auditing using `HttpClient`
* Measures response time
* Returns HTTP status code
* Detects URL reachability
* 10-second configurable timeout
* Memory caching (5 minutes)
* Serilog logging (Console + File)
* Global exception handling
* Request ID middleware
* Rate limiting
* Swagger/OpenAPI documentation
* XML documentation comments
* Configuration using `IOptions`
* Unit testing with xUnit
* RESTful API design
* Git & GitHub integration

---

## Technology Stack

| Technology            | Version  |
| --------------------- | -------- |
| ASP.NET Core          | 8.0      |
| C#                    | 12       |
| Swagger (Swashbuckle) | Latest   |
| Serilog               | Latest   |
| xUnit                 | Latest   |
| Memory Cache          | Built-in |
| HttpClient            | Built-in |
| Git                   | Latest   |

---

## Project Structure

```text
DigitalHeroes.UrlAudit.Api
│
├── Configuration
│   └── AuditSettings.cs
│
├── Controllers
│   └── AuditController.cs
│
├── DTOs
│   ├── AuditRequestDto.cs
│   ├── AuditResponseDto.cs
│   └── ApiErrorResponse.cs
│
├── Middleware
│   ├── ExceptionMiddleware.cs
│   └── RequestIdMiddleware.cs
│
├── Services
│   └── AuditService.cs
│
├── Logs
│
├── Program.cs
├── appsettings.json
└── README.md
```

---

## API Endpoint

### Audit URL

**POST**

```http
/api/Audit
```

### Request

```json
{
  "url": "https://google.com"
}
```

### Success Response

```json
{
  "success": true,
  "url": "https://google.com",
  "statusCode": 200,
  "responseTimeMs": 215,
  "isReachable": true,
  "message": "URL audited successfully"
}
```

### Error Response

```json
{
  "type": "ServerError",
  "title": "Unexpected Error",
  "status": 500,
  "detail": "An unexpected error occurred.",
  "instance": "/api/Audit"
}
```

---

## Architecture

The project follows a layered architecture.

```
Client
   │
   ▼
Controller
   │
   ▼
AuditService
   │
   ▼
HttpClient
   │
   ▼
Target Website
```

Cross-cutting concerns are handled through middleware:

* Exception Middleware
* Request ID Middleware
* Serilog Request Logging

---

## Configuration

Configuration is managed using the Options Pattern.

Example:

```json
{
  "AuditSettings": {
    "TimeoutSeconds": 10,
    "CacheDurationMinutes": 5
  }
}
```

---

## Logging

Serilog writes logs to:

* Console
* Logs/log-yyyyMMdd.txt

Example:

```
Application Starting

Auditing URL https://google.com

Stored audit result in cache

Returning cached result

HTTP POST /api/Audit responded 200
```

---

## Caching

The application stores audit results in memory for **5 minutes**.

Benefits:

* Faster responses
* Reduced network calls
* Better scalability

---

## Request ID

Every request receives a unique `X-Request-ID` header for request tracing and debugging.

Example:

```
X-Request-ID: e9bd9740-1616-48c4-ae3c-1abd4635d841
```

---

## Exception Handling

Global exception middleware returns consistent JSON error responses.

Example:

```json
{
  "type": "ServerError",
  "title": "Unexpected Error",
  "status": 500,
  "detail": "An unexpected error occurred."
}
```

---

## Unit Tests

Implemented using **xUnit**.

Current test coverage includes:

* Successful URL audit
* Cached response verification
* Timeout handling

Run tests:

```bash
dotnet test
```

---

## How to Run

Clone the repository:

```bash
git clone https://github.com/raishettycodes/DigitalHeroes.UrlAudit.Api.git
```

Navigate to the project:

```bash
cd DigitalHeroes.UrlAudit.Api
```

Restore packages:

```bash
dotnet restore
```

Build:

```bash
dotnet build
```

Run:

```bash
dotnet run
```

Open Swagger:

```
https://localhost:<port>/swagger
```

---

## Future Improvements

* Health Checks
* API Versioning
* Response Compression
* Docker Support
* GitHub Actions (CI/CD)
* Authentication & Authorization
* Distributed Caching (Redis)
* Azure Deployment

---

## Repository

GitHub Repository:

https://github.com/raishettycodes/DigitalHeroes.UrlAudit.Api

---

## Author

**Karthik Raishetty**

ASP.NET Core | C# | .NET 8 | REST APIs | SQL Server | Git | GitHub
