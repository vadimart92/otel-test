using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Web.DynamicData;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WebApplication1.Controllers;
using WebApplication1.OTel;
using Process = WebApplication1.OTel.Process;

public class TracingSettings
{
	public TracingDestination Destination { get; set; }
}

public class TracingDestination
{
	
}

public class OpenTelemetryTracingDestination : TracingDestination
{
}
public class InMemoryTracingDestination : TracingDestination
{
	public int RingBufferLength { get; set; } = 100;
}

public class LocalFileTracingDestination : TracingDestination
{
	
}

public class Telemetry
{
	private static TracerProvider _tracerProvider;
	public static readonly ActivitySource ActivitySource;
	private static MeterProvider _meterProvider;

	private static readonly ResourceBuilder _resourceBuilder;
	private static TracingRingBuffer _currentTracingRingBuffer;

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
			/*.AddOtlpExporter(options => {
				options.Protocol = OtlpExportProtocol.Grpc;
			})*/
			.Build();
	}

	public static void StartTracing(TracingSettings settings) {
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
		if (settings.Destination is InMemoryTracingDestination memoryTracingDestination) {
			_currentTracingRingBuffer = new TracingRingBuffer(memoryTracingDestination.RingBufferLength);
			builder = builder.AddInMemoryExporter(_currentTracingRingBuffer);
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

	private static Lazy<Process> _process = new Lazy<Process>(() => {
		var process = new Process();
		process.Initialize(_tracerProvider);
		return process;
	});
	
	public static void WriteTraceData(Stream output, TraceFormat traceFormat) {
		switch (traceFormat) {
			case TraceFormat.JaegerUI:
				WriteJaegerUIJson(output, _process.Value);
				return;
		}
		throw new NotSupportedException();
	}

	private static void WriteJaegerUIJson(Stream output, Process process) {
		using var writer = new Utf8JsonWriter(output);
		writer.WriteStartObject();
		writer.WriteStartArray("data");
		var roots = _currentTracingRingBuffer.Flush();
		foreach (var root in roots) {
			writer.WriteStartObject();
			writer.WriteString("traceID", root.Root.Context.TraceId.ToHexString());
			writer.WriteStartArray("spans");
			WriteSpan(writer, root.Root);
			foreach (var activity in root.Children) {
				WriteSpan(writer, activity);
			}
			writer.WriteEndArray();
			
			writer.WriteStartObject("processes");
			
			writer.WriteStartObject("1");
			process.Write(writer);
			writer.WriteEndObject();
			
			writer.WriteEndObject();
			
			writer.WriteEndObject();
		}
		
		writer.WriteEndArray();
		writer.WriteEndObject();
		writer.Dispose();
	}

	private static void WriteSpan(Utf8JsonWriter writer, Activity activity) {
		writer.WriteStartObject();
		writer.WriteString("processID", "1");
		var jaegerSpan = activity.ToJaegerSpan();
		jaegerSpan.Write(writer);
		jaegerSpan.Return();
		writer.WriteEndObject();
	}
}



class TracingRingBuffer : Collection<Activity>
{
	private readonly int _maxItems;

	public TracingRingBuffer(int maxItems) {
		_maxItems = maxItems;
	}

	protected override void InsertItem(int index, Activity item) {
		if (index > _maxItems) {
			index -= _maxItems;
		}
		base.InsertItem(index, item);
	}

	public IEnumerable<TraceRoot> Flush() {
		var items = Items.ToArray();
		Items.Clear();
		var roots = new Dictionary<string, TraceRoot>();
		int unprocessed;
		do {
			unprocessed = items.Length;
			for (var i = items.Length -1; i >= 0; i--) {
				var activity = items[i];
				if (activity == null) {
					unprocessed--;
					continue;
				}
				if (activity.ParentId != null) {
					if (roots.TryGetValue(activity.RootId, out var root)) {
						root.Children.Add(activity);
						unprocessed--;
						items[i] = null;
					}
					continue;
				}
				var id = activity.RootId;
				roots[id] = new TraceRoot {
					Root = activity
				};
				unprocessed--;
				items[i] = null;
			}
		} while (unprocessed > 0);
		return roots.Values;
	}
}

public class TraceRoot
{
	public Activity Root { get; set; }
	public List<Activity> Children { get; set; } = new List<Activity>();
}


