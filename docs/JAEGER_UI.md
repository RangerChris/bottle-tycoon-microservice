# Jaeger UI Configuration and Theming

## Current Version

**Jaeger Version**: v2 (latest stable)

**Theme Support**: ✅ Native dark/light theme support via configuration

The deployment now uses Jaeger v2 which **supports native theme customization** through the UI configuration file.

## Theme Configuration

### Current Setup - Dark Theme Enabled

The Jaeger UI is configured to use **dark theme** by default through the `jaeger-ui-config.json` file:

```json
{
  "theme": "dark"
}
```

The theme is automatically applied when you access the Jaeger UI at http://localhost:16686.

### Changing the Theme

To switch between light and dark themes, edit `monitoring/jaeger-ui-config.json`:

```json
{
  "theme": "light"  // Change to "light" for light mode
}
```

Then restart Jaeger:
```powershell
docker-compose restart jaeger
```

## Additional Customization Options

### Option 1: Use the Built-in Theme (Current Setup)

### Option 1: Use the Built-in Theme (Current Setup)

Jaeger v2 provides native dark/light theme support. Simply set `"theme": "dark"` or `"theme": "light"` in the UI config.

**Benefits**:
- No browser extensions needed
- Consistent experience across all browsers
- Officially supported by Jaeger

### Option 2: Use Grafana for Enhanced Visualization

Grafana provides additional theming options and can display Jaeger traces with more customization.

**Access Grafana**: http://localhost:3001 (username: admin, password: admin)

**To view traces in Grafana**:
1. Navigate to **Explore** (compass icon)
2. Select **Jaeger** as the data source
3. Use the query builder or trace ID search
4. Switch themes: User icon → Preferences → Theme

## Jaeger v2 Configuration

### Architecture

Jaeger v2 is built on the OpenTelemetry Collector architecture, providing:
- Modern trace processing pipelines
- Better performance and scalability
- Unified configuration format
- Multiple receiver protocols (OTLP, Jaeger, Zipkin)

### Configuration File Structure

The `monitoring/jaeger-config.yaml` defines:

**Service Pipeline**:
```yaml
service:
  extensions: [jaeger_storage, jaeger_query, healthcheckv2]
  pipelines:
    traces:
      receivers: [otlp, jaeger, zipkin]
      processors: [batch]
      exporters: [jaeger_storage_exporter]
```

**Storage**:
- In-memory storage (default, 100k traces max)
- Configurable backends for production use

**Receivers**:
- OTLP (gRPC: 4317, HTTP: 4318)
- Jaeger (gRPC: 14250, HTTP: 14268)
- Zipkin (9411)

**UI Configuration**:
- Theme settings
- Custom menu links
- Link patterns for logs/metrics integration

## Current UI Configuration

The `monitoring/jaeger-ui-config.json` file provides these customizations:

### Theme Setting
```json
{
  "theme": "dark"  // "dark" or "light"
}
```

### Custom Menu Links
- Quick access to Jaeger documentation
- Links to Bottle Tycoon project
- Direct link to Grafana for alternative trace viewing

### Link Patterns
Clicking on service names in traces provides quick links to:
- **View Logs in Grafana**: See correlated logs filtered by service
- **View Metrics in Prometheus**: View service-specific metrics

### Full Configuration Example
```json
{
  "theme": "dark",
  "menu": [
    {
      "label": "About Jaeger",
      "url": "https://www.jaegertracing.io/",
      "newWindow": true
    }
  ],
  "linkPatterns": [
    {
      "type": "logs",
      "key": "service",
      "url": "http://localhost:3001/explore?...",
      "text": "View Logs in Grafana"
    }
  ],
  "dependencies": {
    "dagMaxNumServices": 50,
    "menuEnabled": true
  }
}
```

## Accessing Observability Tools

### Jaeger UI
- **URL**: http://localhost:16686
- **Purpose**: Distributed tracing
- **Theme**: Dark theme (configurable via UI config)
- **Version**: Jaeger v2

### Grafana
- **URL**: http://localhost:3001
- **Credentials**: admin / admin
- **Features**: Traces, logs, metrics in one unified dashboard

### Prometheus
- **URL**: http://localhost:9090
- **Purpose**: Metrics and queries

## Troubleshooting

### Configuration Not Loading

**Symptom**: Custom menu links, link patterns, or theme not appearing

**Solution**:
1. Verify both config files exist and are valid:
   ```powershell
   docker exec bottle-tycoon-jaeger cat /etc/jaeger/config.yaml
   docker exec bottle-tycoon-jaeger cat /etc/jaeger/jaeger-ui-config.json
   ```
2. Check for YAML/JSON syntax errors
3. Restart Jaeger:
   ```powershell
   docker-compose restart jaeger
   ```
4. Clear browser cache (Ctrl+Shift+Delete)

### Theme Not Applying

**Symptom**: UI still shows light theme

**Solution**:
1. Verify the theme setting in UI config:
   ```powershell
   docker exec bottle-tycoon-jaeger cat /etc/jaeger/jaeger-ui-config.json | grep theme
   ```
2. Ensure the file has UTF-8 encoding without BOM
3. Restart container:
   ```powershell
   docker-compose restart jaeger
   ```
4. Hard refresh browser (Ctrl+F5)

### Jaeger Container Won't Start

**Symptom**: Container exits immediately or shows errors

**Solution**:
1. Check logs:
   ```powershell
   docker logs bottle-tycoon-jaeger
   ```
2. Validate YAML configuration:
   ```powershell
   # Check for syntax errors in config.yaml
   docker run --rm -v ${PWD}/monitoring/jaeger-config.yaml:/config.yaml mikefarah/yq eval /config.yaml
   ```
3. Verify all required ports are available (not used by other processes)

### Services Not Sending Traces

**Symptom**: No traces appearing in Jaeger UI

**Solution**:
1. Verify services are configured with correct OTLP endpoint:
   - Services should use `http://jaeger:4318` (Docker network)
   - Or `http://localhost:4318` (host network)
2. Check service logs for OpenTelemetry errors
3. Verify OTLP receiver is enabled in Jaeger config
4. Test connectivity:
   ```powershell
   curl http://localhost:4318/v1/traces -X POST -H "Content-Type: application/json" -d '{}'
   ```

## References

- [Jaeger v2 Documentation](https://www.jaegertracing.io/docs/2.0/)
- [Jaeger v2 Deployment Guide](https://www.jaegertracing.io/docs/2.0/deployment/)
- [OpenTelemetry Collector Configuration](https://opentelemetry.io/docs/collector/configuration/)
- [Grafana Trace Visualization](https://grafana.com/docs/grafana/latest/datasources/jaeger/)