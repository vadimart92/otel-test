using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Web;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WebApplication1.Controllers;

class Telemetry
{
	private static TracerProvider _tracerProvider;
	public static readonly ActivitySource ActivitySource;
	private static MeterProvider _meterProvider;

	private static readonly ResourceBuilder _resourceBuilder;

	public static List<MetricSnapshot> Metrics { get; private set; }
	public static Meter Meter { get; private set; }

	static Telemetry() {
		ActivitySource = new ActivitySource($"creatio-{new DirectoryInfo(Environment.CurrentDirectory).Parent.Name}");
		Meter = new Meter(ActivitySource.Name);
		_resourceBuilder = ResourceBuilder.CreateDefault()
			.AddService(serviceName: ActivitySource.Name, serviceVersion: "1.0.0");
	}

	public static void InitMetrics() {
		Metrics = new List<MetricSnapshot>();
		var exporter = new PrometheusExporter(new PrometheusExporterOptions {
			ScrapeEndpointPath = "metrics",
			HttpListenerPrefixes = new []{"http://localhost:5000/"}
		});
		var reader = new BaseExportingMetricReader(exporter);
		reader.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
		_meterProvider = Sdk.CreateMeterProviderBuilder()
			.AddAspNetInstrumentation()
			.SetResourceBuilder(_resourceBuilder)
			.AddMeter(Meter.Name)
			.AddInMemoryExporter(Metrics, options => {
				options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
			})
			.AddReader(reader)
			/*
			.AddPrometheusExporter(options => {
				options.ScrapeEndpointPath = "otelMetrics";
			})*/
			/*.AddOtlpExporter(options => {
				options.Protocol = OtlpExportProtocol.Grpc;
			})*/
			.Build();
	}

	public static void StartTracing(bool inMemory) {
		var builder = Sdk.CreateTracerProviderBuilder()
			.AddAspNetInstrumentation(options => {
				options.RecordException = true;
				options.Filter = context => true;
				options.Enrich = (activity, eventName, rawObject) => {
					switch (eventName) {
						case "OnStartActivity": {
							if (rawObject is HttpRequest httpRequest) {
								activity?.SetTag("AnonymousID", httpRequest.AnonymousID);
							}
							break;
						}
						case "OnStopActivity": {
							if (rawObject is HttpResponse httpResponse) {
								activity?.SetTag("responseType", httpResponse.ContentType);
							}
							break;
						}
					}
				};
			})
			.AddSource(ActivitySource.Name)
			.SetResourceBuilder(_resourceBuilder);
		if (inMemory) {
			var exportedItems = new List<Activity>();
			builder = builder.AddInMemoryExporter(exportedItems);
		} else {
			builder = builder.AddOtlpExporter(options => {
				//options.Endpoint = new Uri("http://localhost:4317");
				options.Protocol = OtlpExportProtocol.Grpc;
			});
		}
		_tracerProvider = builder.Build();
	}

	public static void StopTracing() {
		_tracerProvider?.Dispose();
		_tracerProvider = null;
	}

	public static void Dispose() {
		StopTracing();
		_meterProvider?.Dispose();
	}

	public static void WriteTraceData(Stream output, TraceFormat jsonJaeger) {
		throw new NotImplementedException();
	}
}
