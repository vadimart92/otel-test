using System;
using System.Web.Mvc;

namespace WebApplication1.Controllers;

public class TelemetryController : Controller
{
	public void Start(bool inMemory = false) {
		Telemetry.StartTracing(new TracingSettings() {
			Destination = inMemory ? new InMemoryTracingDestination() : new OpenTelemetryTracingDestination()
		});
	}

	public void Stop() {
		Telemetry.StopTracing();
	}

	public void GetTraceData(string format = nameof(TraceFormat.JaegerUI)) {
		Response.AddHeader("Content-Type", "application/json");
		Telemetry.WriteTraceData(Response.OutputStream, (TraceFormat)Enum.Parse(typeof(TraceFormat), format));
	}
}

public enum TraceFormat
{
	JaegerUI,
	Zipkin
}
