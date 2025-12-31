# Health Check Endpoints Setup

## Overview

The server exposes two health check endpoints:
- `/health` - Readiness probe (checks server + Redis) - **Requires API key**
- `/alive` - Liveness probe (checks only server process) - **No API key required**

## Configuration

### Setting the API Key

**For Development (no authentication):**
Leave the `HealthCheck:ApiKey` empty in `appsettings.Development.json`:
```json
{
  "HealthCheck": {
    "ApiKey": ""
  }
}
```

**For Production (with authentication):**

1. **Using appsettings.json:**
```json
{
  "HealthCheck": {
    "ApiKey": "your-secret-key-here"
  }
}
```

2. **Using Environment Variable:**
```bash
export HealthCheck__ApiKey="your-secret-key-here"
```

3. **Using Docker Compose:**
Edit `.env` file:
```bash
HEALTH_CHECK_API_KEY=your-secret-key-here
```

## Usage

### Without API Key (Development or when ApiKey is empty)

```bash
# Check readiness (server + Redis)
curl http://localhost:8080/health

# Check liveness (server only)
curl http://localhost:8080/alive
```

### With API Key (Production)

```bash
# Check readiness (requires API key)
curl -H "X-Health-Check-Key: your-secret-key-here" https://yzportalserver.38569123.xyz/health

# Check liveness (no API key needed)
curl https://yzportalserver.38569123.xyz/alive
```

## Response Format

### Success Response (200 OK)
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "self",
      "status": "Healthy",
      "exception": null,
      "duration": "00:00:00.0001234"
    }
  ]
}
```

### Unhealthy Response (503 Service Unavailable)
```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "self",
      "status": "Healthy",
      "exception": null,
      "duration": "00:00:00.0001234"
    },
    {
      "name": "redis",
      "status": "Unhealthy",
      "exception": "Redis unavailable",
      "duration": "00:00:05.0000000"
    }
  ]
}
```

### Unauthorized Response (401 Unauthorized)
When API key is required but not provided or incorrect:
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

## Uptime Kuma Setup

### For /health endpoint (with API key):

1. **Monitor Type:** HTTP(s)
2. **URL:** `https://yzportalserver.38569123.xyz/health`
3. **Headers:**
   ```
   X-Health-Check-Key: your-secret-key-here
   ```
4. **Expected Status Code:** 200
5. **Check Interval:** 60 seconds (recommended)

### For /alive endpoint (without API key):

1. **Monitor Type:** HTTP(s)
2. **URL:** `https://yzportalserver.38569123.xyz/alive`
3. **Expected Status Code:** 200
4. **Check Interval:** 60 seconds

## Caching Behavior

**AllowCachingResponses = false** for both endpoints because:
- Health status changes in real-time (Redis connection can fail/recover)
- Monitoring tools need fresh data, not cached responses
- The built-in OutputCache policy (10 seconds) already handles request throttling
- HTTP Cache-Control headers won't cache stale health data

## Security Notes

1. **API Key Storage:**
   - Never commit API keys to source control
   - Use environment variables or secret managers in production
   - Rotate keys periodically

2. **Additional Security Options:**
   - Add IP whitelisting in your reverse proxy (Caddy/Nginx)
   - Use HTTPS only in production
   - Consider separate monitoring subnet/VPN

3. **Liveness Endpoint:**
   - `/alive` has no authentication to ensure container orchestrators can always check process health
   - This is intentional - even if Redis is down, the container shouldn't restart

## Troubleshooting

**401 Unauthorized on /health:**
- Check if `HealthCheck__ApiKey` environment variable is set
- Verify the `X-Health-Check-Key` header matches the configured key
- Check logs for authorization failures

**503 Service Unavailable:**
- Check individual health check status in the JSON response
- Look for exceptions in the response
- Verify Redis connectivity if redis check is failing

**Connection refused:**
- Ensure the server is running
- Check firewall rules
- Verify the correct port is exposed
