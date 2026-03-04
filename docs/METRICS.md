# Metrics Guide — Frontend → Service → Grafana

This document describes a practical, end-to-end approach to collecting, exporting, storing, and visualizing metrics for the Bottle Tycoon system. It covers frontend telemetry, instrumentation inside .NET services, Prometheus scraping, and building Grafana dashboards and alerts.

Goals
- Capture useful operational and business metrics across frontend and services.
- Keep label cardinality low and metrics meaningful for monitoring and alerting.
- Use OpenTelemetry for consistent instrumentation and Prometheus for storage and Grafana for visualization.

Quick overview of components
- Frontend: collect browser performance & custom business events and send them to a telemetry endpoint or an OTLP collector.
- Service (.NET): instrument HTTP, database, background jobs and business metrics using System.Diagnostics.Metrics + OpenTelemetry; expose metrics to Prometheus via OpenTelemetry Prometheus exporter.
- Prometheus: scrape `/metrics` endpoints from services and exporters.
- Grafana: dashboards, panels, alerts, and provisioning of dashboards from JSON files.

Table of contents
1. Principles & naming
2. Frontend telemetry (browser)
3. Instrumenting .NET services (what the repo actually implements)
4. Prometheus setup and scrape targets
5. Grafana dashboards, example queries and alerts
6. Metrics best practices and troubleshooting (notes from code review)

---

1) Principles & naming
- Metric types
  - Counter: monotonically increasing counts (e.g., `deliveries_processed_total`).
  - Histogram: record distributions (latencies, sizes) and enable quantiles/rate calculations in Prometheus.
  - ObservableGauge (or Gauge): current state values (queue length, current load).
  - UpDownCounter: for values that can go up and down (rare; prefer ObservableGauge for instantaneous values).
- Naming
  - Use snake_case and suffix with units when applicable (e.g., `_seconds`, `_bytes`).
  - Use `_total` suffix for counters (e.g., `truck_deliveries_total`).
  - Prefix business metrics with the service name to avoid collisions (e.g., `truckservice_deliveries_total`).
- Labels (dimensions)
  - Keep cardinality low. Typical labels: service, environment, instance, job, status_code, method, route.
  - Avoid user IDs or unique identifiers in labels.
- Meters & Scopes
  - Register a Meter per service (e.g., `new Meter("TruckService")`).
  - Group business metrics under that meter.

---

2) Frontend telemetry (browser)
There are two reliable patterns for frontend metrics:
- A: Use an OTLP-compatible JS SDK to send metrics to a collector (OpenTelemetry JS). The collector receives OTLP and forwards to an OTLP receiver, or exports directly to a backend.
- B: Collect minimal metrics in the browser (Web Vitals, custom events) and POST them to a backend telemetry endpoint (e.g., `POST /telemetry/browser`) where the service translates those into Prometheus metrics via OpenTelemetry/Metric instruments.

Recommended simple approach (B) for this repo: collect metrics in the browser and send aggregated events to a telemetry endpoint. This avoids having to run an OTLP collector in local dev.

Example: collect Web Vitals and send to `POST /telemetry/browser`
```javascript
// uses web-vitals (npm i web-vitals)
import {getCLS, getFID, getLCP} from 'web-vitals';

function sendToBackend(name, value, extra = {}) {
  navigator.sendBeacon('/telemetry/browser', JSON.stringify({ name, value, extra }));
}

getCLS(metric => sendToBackend(metric.name, metric.value));
getFID(metric => sendToBackend(metric.name, metric.value));
getLCP(metric => sendToBackend(metric.name, metric.value));

// Example for a business event:
function reportTruckDispatch(truckId, recyclerId, load) {
  // Keep payload small and anonymous — do NOT send high-cardinality identifiers as labels
  navigator.sendBeacon('/telemetry/browser', JSON.stringify({
    name: 'truck_dispatch',
    value: 1,
    extra: { load }
  }));
}
```

Backend handling: implement an endpoint that receives these payloads and increments counters or records histograms. For example, in .NET you could accept a POST and call a Meter's Counter.

Notes
- Use sendBeacon for fire-and-forget on page unload; otherwise batching + debounce.
- For privacy and cardinality reasons, avoid sending sensitive or high-cardinality fields directly as labels; include them in the payload body only if needed and aggregate in the backend.

---

3) Instrumenting .NET services (what the repo actually implements)
This section describes how the code in this repository currently implements metrics and where it differs from an ideal, fully standardized setup.

Summary of repository status
- All major services (GameService, RecyclerService, TruckService, RecyclingPlantService, HeadquartersService) configure OpenTelemetry and register metrics via `builder.Services.AddOpenTelemetry().WithMetrics(...).AddPrometheusExporter()`.
- Each service calls `app.UseOpenTelemetryPrometheusScrapingEndpoint()` to expose an internal scrape endpoint for Prometheus.
- Several services create and register a `Meter` instance and a small helper metrics class that creates instruments (ObservableGauges / Counters / Histograms):
  - `RecyclerService` registers a `Meter("RecyclerService")` and `RecyclerMetrics` (metrics: `recycler_current_bottles`, `recycler_current_visitors`, `recycler_queue_depth`).
  - `TruckService` registers `Meter("TruckService")` and `TruckMetrics` (metrics: `truck_current_load`, `truck_capacity`, `truck_deliveries`).
  - `GameService` exposes `GameMetrics` which creates an observable gauge `player_total_earnings` via a static Meter inside the class (note: this is implemented differently from the other services).
  - `RecyclingPlantService` and `HeadquartersService` configure OpenTelemetry and the Prometheus exporter but their service-specific metrics helpers are placed in their services when needed.

Concrete metric names found in code
- truck_current_load (ObservableGauge per truck, labeled with truck_id and truck_name)
- truck_capacity (ObservableGauge per truck, labeled with truck_id and truck_name)
- truck_deliveries (Counter)
- recycler_current_bottles (ObservableGauge per recycler)
- recycler_current_visitors (ObservableGauge per recycler)
- recycler_queue_depth (ObservableGauge per recycler)
- bottles_processed (created in code as a meter counter in RecyclerService)
- player_total_earnings (ObservableGauge created in GameMetrics)

How the code wires metrics (patterns observed)
- Many services create the Meter before calling `AddOpenTelemetry()` and register it in DI via `builder.Services.AddSingleton(meter)`. The meter name is then added to OpenTelemetry via `.WithMetrics(metrics => metrics.AddMeter(meterName))` so the exporter picks up those instruments.
- Metrics helpers (e.g., `TruckMetrics`, `RecyclerMetrics`) are registered as singletons and, when constructed, create ObservableGauges that enumerate snapshots from an in-memory telemetry store or DB-backed list. This pattern ensures that ObservableGauges return one Measurement per entity.
- The Prometheus exporter used across the repo is `OpenTelemetry.Exporter.Prometheus.AspNetCore` and the scraping endpoint is exposed with `app.UseOpenTelemetryPrometheusScrapingEndpoint()`.

Differences from the written guidance in this doc
- Meter creation/DI: the doc suggests registering a Meter once and injecting it into helper classes. The repo mostly follows this (RecyclerService, TruckService) but `GameMetrics` creates a static Meter internally instead of relying on a DI-registered Meter. Functionally it's acceptable, but it is inconsistent and is worth standardizing.
- High-cardinality labels: the code uses per-entity labels in several places (truck_id, recycler_id, truck_name, recycler_name). These are low-cardinality in practice (small fleets / number of recyclers) and acceptable. However, `GameMetrics` currently exposes `player_total_earnings` with a `player_id` label on an ObservableGauge — this risks high cardinality if the number of players can grow large. The doc's recommendation to avoid user IDs in labels should be highlighted: if `player_id` can be numerous, prefer aggregations (per tier, per region) or push per-player data to a long-term store instead of Prometheus labels.

Recommendations based on code review
- Standardize meter registration: create and register the Meter in Program.cs for each service and inject it into the metrics helper classes to keep the pattern consistent (e.g., what `TruckService` and `RecyclerService` already do).
- Replace or augment `player_total_earnings{player_id=...}` with a low-cardinality alternative (for example `player_earnings_bucket{tier="free|pro"}` or aggregate metrics like `player_total_earnings_sum` / `player_total_earnings_count` for histogram-like aggregation). If per-player time-series is critical, export those to a user-focused analytics store (e.g., ClickHouse, a DB), not Prometheus labels.
- Keep ObservableGauges enumerating per-entity snapshots when the number of entities is bounded and small (trucks, recyclers). That's what the current code implements.

Additions to the example code in this document
- The repository already includes working examples of:
  - Creating a Meter and registering it in DI (see `RecyclerService.Program.cs` and `TruckService.Program.cs`).
  - Creating ObservableGauges that return a sequence of `Measurement<T>` entries, one per entity (see `TruckMetrics` and `RecyclerMetrics`).

Example (observed pattern — simplified)
```csharp
// Program.cs
var meter = new Meter("RecyclerService");
builder.Services.AddSingleton(meter);

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("RecyclerService")
        .AddPrometheusExporter());

// RecyclerMetrics.cs (registered singleton)
public RecyclerMetrics(Meter meter, IRecyclerTelemetryStore store, IServiceProvider sp) {
    meter.CreateObservableGauge("recycler_current_bottles", () =>
        store.GetAll().Select(s => new Measurement<int>(s.CurrentBottles, new KeyValuePair<string, object?>("recycler_id", s.RecyclerId.ToString()))),
        "bottles");
}
```

---

4) Prometheus configuration (scrape targets)
Prometheus must be configured to scrape each service. Example snippet for `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'truckservice'
    static_configs:
      - targets: ['truckservice:80']
  - job_name: 'grafana'
    static_configs:
      - targets: ['grafana:3000']
  - job_name: 'loki'
    static_configs:
      - targets: ['loki:3100']
```

Notes:
- Use internal docker-compose hostnames (service names) for containerized scraping.
- For OpenTelemetry Prometheus exporter, `UseOpenTelemetryPrometheusScrapingEndpoint()` exposes a dedicated scrape endpoint inside the app container; ensure Prometheus can reach it.

---

5) Grafana: dashboards, queries and alerts
A. Dashboard placement & provisioning
- Store dashboards under `monitoring/grafana/dashboards/` and provision them in Grafana using provisioning files in `monitoring/grafana/provisioning/`.
- This repo already contains example dashboard JSON files; import them or restart Grafana after editing.

B. Useful Prometheus queries
- Request rate:
  - `rate(truckservice_http_server_requests_total[5m])`
- Error rate (5m):
  - `sum(rate(truckservice_http_server_requests_total{status=~"5.."}[5m])) / sum(rate(truckservice_http_server_requests_total[5m]))`
- Gauge snapshot (current):
  - `truck_current_load{truck_name="Truck 1"}` or aggregate: `sum(truck_current_load) by (truck_name)`
- Business metric rate (deliveries per minute):
  - `rate(truck_deliveries[5m])`

C. Panel types
- Use `stat` panels for service health (up / down), `timeseries` for request rates and latencies, and `gauge`/`stat` for current counts.

D. Alerts
- Example alert: service down
  - Expr: `sum(up{job="truckservice"}) == 0`
  - For resilience, use additional evaluation windows (e.g., 3m) to avoid flapping.
- Example alert: high error rate
  - Expr: `sum(rate(truckservice_http_server_requests_total{status=~"5.."}[5m])) / sum(rate(truckservice_http_server_requests_total[5m])) > 0.05`

E. Panel labels & legends
- Use human-readable names for label values (this repo's convention). Avoid GUIDs in labels; if needed, map them to friendly names before recording.

---

6) Best practices, pitfalls & troubleshooting (notes from code review)
This section contains practical notes that come from reviewing the actual repository implementation.

A. Cardinality
- High-cardinality labels are the primary cause of Prometheus blowup. The code is careful in most places (trucks and recyclers are few), but `GameMetrics` currently creates a `player_total_earnings` ObservableGauge labeled by `player_id` (see `src/GameService/Services/GameMetrics.cs`). If your player base can grow large, this will cause cardinality problems. Consider switching to aggregated metrics or exporting per-player analytics to a different store.

B. Meter registration patterns
- The repo mostly follows the recommended pattern: create a `Meter` in Program.cs, register it in DI, and add the meter name to OpenTelemetry `.WithMetrics(...).AddMeter(meterName)`.
- Some helper classes (GameMetrics) create a static Meter internally. Standardize on the DI pattern for consistency.

C. Units & suffixes
- Include units in metric names and use consistent units across services (seconds for time, bytes for sizes). The repo uses `"bottles"` and `"credits"` as units for gauges where appropriate.

D. Aggregation strategy
- Record raw histograms in services; compute percentiles and rates in Prometheus with `histogram_quantile` and `rate`.

E. Test locally
- Use `curl http://localhost:<port>/metrics` to confirm metrics are exposed.
- Use Grafana import or provisioning to validate dashboards.

F. Example troubleshooting checklist
- No metrics in Prometheus: check service is exposing `/metrics` and Prometheus scrape target is configured and reachable.
- Dashboards empty: check metric names and label names; the dashboard queries must match actual metric names exported by the repo's OpenTelemetry configuration.

---

Appendix: Example end-to-end flow (dispatch event)
1. Frontend triggers dispatch: calls `POST /dispatch` on Headquarters
2. Headquarters service records:
   - `headquarters_dispatches_total{target_recycler="NorthRecycler"}` counter ++
   - `headquarters_dispatch_latency_seconds` histogram records processing time
3. TruckService receives `POST /trucks/{id}/telemetry` and an ObservableGauge `truck_current_load` is updated; Prometheus scrapes and Grafana panels update.

---

Quick diagnostic commands (PowerShell; Windows dev setup)
```powershell
# Check metrics endpoint
Invoke-WebRequest -Uri http://localhost:5003/metrics | Select-Object -First 2

# Restart Grafana (docker-compose)
docker-compose restart grafana

# Tail Grafana logs
docker logs bottle-tycoon-grafana --tail 200
```

---

If you want, I can:
- Standardize meter registration across services (create and register `Meter` in Program.cs for all services) and update code in a PR.
- Replace the `player_total_earnings{player_id=...}` metric with a low-cardinality alternative and add a migration plan.
- Add a telemetry controller to accept browser telemetry and aggregate it into counters in a selected service.

File updated: `docs/METRICS.md` — I kept the original guidance but updated the document to explicitly reflect the repository's current implementation and added recommendations where code deviates from best practice.