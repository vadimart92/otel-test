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

class TracingSettings
{
	public TracingDestination Destination { get; set; }
}

class TracingDestination
{
	
}

class OpenTelemetryTracingDestination : TracingDestination
{
}
class InMemoryTracingDestination : TracingDestination
{
	public int RingBufferLength { get; set; }
}

class LocalFileTracingDestination : TracingDestination
{
	
}

class Telemetry
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
			/*
			.AddPrometheusExporter(options => {
				options.ScrapeEndpointPath = "otelMetrics";
			})*/
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

	public static void WriteTraceData(Stream output, TraceFormat traceFormat) {
		var resource = _tracerProvider.GetResource();
		switch (traceFormat) {
			case TraceFormat.JaegerUI:
				var writer = new Utf8JsonWriter(output);
				writer.WriteStartArray("data");
				writer.WriteStartObject();
				var activities = _currentTracingRingBuffer.Flush();
				if (activities.Any()) {
					var first = activities.First();
					writer.WriteString("traceID", first.Context.TraceId.ToHexString());
					writer.WriteStartArray("spans");
					foreach (var activity in activities) {
						writer.WriteStartObject();
						writer.WriteString("processID", "1");
						new JaegerUIActivity(activity).Write(writer);
						writer.WriteEndObject();
					}
					writer.WriteEndArray();
					writer.WriteStartArray("processes");
					writer.WriteStartObject();
					string serviceName = null;
					string serviceNameSpace = null;
					var tags = new List<JaegerTag>();
					foreach (var label in resource.Attributes) {
						if (label.Key == "service.name") {
							serviceName = label.Value.ToString();
						}
						if (label.Key == "service.namespace") {
							serviceNameSpace = serviceName;
						}
						if (JaegerTag.TryGet(label, out var tag)) {
							tags.Add(tag);
						}
					}
					writer.WriteString("serviceName", serviceNameSpace != null ? $"{serviceNameSpace}.{serviceName}" : serviceName);
					writer.WriteStartArray("tags");
					foreach (var tag in tags) {
						writer.WriteStartObject();
						tag.Write(writer);
						writer.WriteEndObject();
					}
					writer.WriteEndArray();
					writer.WriteEndObject();
					writer.WriteEndArray();
				}
				writer.WriteEndObject();
				writer.WriteEndArray();
				break;
		}
		throw new NotSupportedException();
	}

	readonly struct  JaegerTag
	{
		public static bool TryGet(KeyValuePair<string, object> source, out JaegerTag tag) {
			if (source.Value is null) {
				tag = default;
				return false;
			}
			tag = new JaegerTag(source.Key, source.Value);
			return false;
		}

		public JaegerTag(string key, object value) {
			Key = key;
			Value = value;
			Type = JaegerTagType.STRING;
		}
		public string Key { get; }
		public JaegerTagType Type { get; }
		public object Value { get; }

		public void Write(Utf8JsonWriter writer) {
			writer.WriteString("key", Key);
			writer.WriteString("type", Type.ToString().ToLower());
			writer.WriteString("value", Value.ToString());
		}
	}
	
	internal enum JaegerTagType
	{
		/// <summary>
		/// Tag contains a string.
		/// </summary>
		STRING = 0,

		/// <summary>
		/// Tag contains a double.
		/// </summary>
		DOUBLE = 1,

		/// <summary>
		/// Tag contains a boolean.
		/// </summary>
		BOOL = 2,

		/// <summary>
		/// Tag contains a long.
		/// </summary>
		LONG = 3,

		/// <summary>
		/// Tag contains binary data.
		/// </summary>
		BINARY = 4,
	}

	readonly ref struct JaegerUIActivity
	{
		private readonly Activity _activity;

		public JaegerUIActivity(Activity activity) {
			_activity = activity;
			//todo
		}

		public void Write(Utf8JsonWriter writer) {
			
		}
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

	public Activity[] Flush() {
		var items = Items.ToArray();
		Items.Clear();
		return items;
	}
}


