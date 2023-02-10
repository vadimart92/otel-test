using System.Web.Mvc;

namespace WebApplication1.Controllers;

public class TelemetryController : Controller
{
	public void Start(bool inMemory = false) {
		Telemetry.StartTracing(inMemory);
	}

	public void Stop() {
		Telemetry.StopTracing();
	}

	public void GetTraceData() {
		Telemetry.WriteTraceData(Response.OutputStream, TraceFormat.JsonJaeger);
	}
}

public enum TraceFormat
{
	JsonJaeger
}
