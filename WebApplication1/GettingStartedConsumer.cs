using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace WebApplication1;
public class GettingStarted
{
	public string Message { get; set; } 
}
public class GettingStartedConsumer :
	IConsumer<GettingStarted>
{
	readonly ILogger<GettingStartedConsumer> _logger;

	public GettingStartedConsumer(ILogger<GettingStartedConsumer> logger)
	{
		_logger = logger;
	}

	public Task Consume(ConsumeContext<GettingStarted> context)
	{
		using var activity = Telemetry.ActivitySource.StartActivity("MyTestConsumer-Save");
		_logger.LogInformation("Received Text: {Text}", context.Message.Message);
		return Task.CompletedTask;
	}
}
