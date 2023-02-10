using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using System.Web.Mvc;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace WebApplication1.Controllers
{
	public class TestController : Controller
	{
		
		public async Task<string> Hello(string name, string schema) {
			await Task.Delay(10);
			MyHelper.Save(name, schema);
			return $"Hello {name}";
		}
	}

	class MyHelper
	{
		public static void Save(string entityName, string schemaName) {
			using var activity = Telemetry.ActivitySource.StartActivity("MyHelper");
			var entity = new MyEntity() {
				SchemaName = schemaName,
				Name = entityName
			};
			entity.Save();
		}
	}

	class MyEntity
	{
		private static readonly Counter<long> _saveCounter = Telemetry.Meter.CreateCounter<long>("EntitySavesCount");
		private static readonly Histogram<float> _saveDuration = Telemetry.Meter.CreateHistogram<float>("EntitySaveDuration");
		
		public string Name { get; set; }
		public string SchemaName { get; set; }
		public void Save() {
			using var activity = Telemetry.ActivitySource.StartActivity("Entity-Save");
			activity?.SetTag("operation", "Save");
			if (activity?.IsAllDataRequested == true)
				activity.SetTag("Name", Name);
			try {
				var time = Stopwatch.GetTimestamp();
				if (Name == "err") {
					throw new Exception();
				}
				MvcApplication.ServiceProvider.GetService<IBus>().Publish(new GettingStarted() {
					Message = $"message from {Name}"
				});
				activity?.SetStatus(ActivityStatusCode.Ok);
				_saveCounter.Add(1, new KeyValuePair<string, object>("SchemaName", SchemaName));
				_saveDuration.Record(Stopwatch.GetTimestamp() - time);
			} catch (Exception) {
				activity?.SetStatus(ActivityStatusCode.Error);
				throw;
			}
		}
	}
}
