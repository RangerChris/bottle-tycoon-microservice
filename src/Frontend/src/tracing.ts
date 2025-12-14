import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { SimpleSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch';
import { registerInstrumentations } from '@opentelemetry/instrumentation';

export function initTracing() {
  const exporter = new OTLPTraceExporter({
    url: import.meta.env.VITE_OTEL_EXPORTER_OTLP_ENDPOINT || '/api/traces',
  });

  const provider = new WebTracerProvider({
    spanProcessors: [new SimpleSpanProcessor(exporter)],
  });

  provider.register();

  registerInstrumentations({
    instrumentations: [
      new FetchInstrumentation({
        propagateTraceHeaderCorsUrls: [/localhost/, /127\.0\.0\.1/, /bottle-tycoon/],
      }),
    ],
  });
}