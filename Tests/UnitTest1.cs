using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WebApplication1.Controllers;

namespace Tests;

public class Tests
{
	[SetUp]
	public void Setup() { }

	[Test]
	public async Task Test1() {
		Telemetry.StartTracing(new TracingSettings() {
			Destination = new InMemoryTracingDestination() 
		});
		GenerateTraces();
		await Task.Delay(TimeSpan.FromSeconds(15));
		var output = new MemoryStream();
		Telemetry.WriteTraceData(output, TraceFormat.JaegerUI);
		var result = Encoding.UTF8.GetString(output.ToArray());
	}

	private static void GenerateTraces() {
		using var root = Telemetry.ActivitySource.CreateActivity("TracesGen", ActivityKind.Internal);
		Activity.Current = root;
		for (int i = 0; i < 100; i++) {
			using var activity = Telemetry.ActivitySource.CreateActivity($"Test-{i}", ActivityKind.Internal, parentContext: root.Context);
			activity?.SetTag("Number", i);
		}
	}
}