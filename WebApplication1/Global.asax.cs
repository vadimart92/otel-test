using System;
using System.Web.Mvc;
using System.Web.Routing;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WebApplication1
{


	public class MvcApplication : System.Web.HttpApplication
	{
		public static IServiceProvider ServiceProvider { get; set; }
		
		protected void Application_Start(object sender, EventArgs e) {
			
			AreaRegistration.RegisterAllAreas();
			FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			Telemetry.InitMetrics();
			var services = new ServiceCollection();
			services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Trace));
			services.AddMassTransit(x => {
				x.AddConsumer<GettingStartedConsumer>();
				x.UsingRabbitMq((context,cfg) =>
				{
					cfg.Host("localhost", "/", h => {
						h.Username("guest");
						h.Password("guest");
					});
					cfg.ConfigureEndpoints(context);
				});
			});
			ServiceProvider = services.BuildServiceProvider();
			ServiceProvider.GetRequiredService<IBusControl>().Start();
		}

		protected void Application_End() {
			Telemetry.Dispose();
			ServiceProvider.GetRequiredService<IBusControl>().Stop();
		}
	}
}
