using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using OpenTelemetry;

namespace WebApplication1.OTel;

internal sealed class Process
{
	public string ServiceName { get; set; }

	public Dictionary<string, JaegerTag> Tags { get; set; }

	public override string ToString()
	{
		var sb = new StringBuilder("Process(");
		sb.Append(", ServiceName: ");
		sb.Append(ServiceName);

		if (Tags != null)
		{
			sb.Append(", Tags: ");
			sb.Append(Tags);
		}

		sb.Append(')');
		return sb.ToString();
	}

	public void Write(Utf8JsonWriter writer) {
		writer.WriteString("serviceName", ServiceName);
		writer.WriteStartArray("tags");
		foreach (var tag in Tags.Values) {
			writer.WriteStartObject();
			tag.Write(writer);
			writer.WriteEndObject();
		}
		writer.WriteEndArray();
	}

	public void Initialize(BaseProvider provider) {
		var resource = provider.GetResource();

		string serviceName = null;
		string serviceNamespace = null;
		foreach (var label in resource.Attributes) {
			string key = label.Key;

			if (label.Value is string strVal) {
				switch (key) {
					case ResourceSemanticConventions.AttributeServiceName:
						serviceName = strVal;
						continue;
					case ResourceSemanticConventions.AttributeServiceNamespace:
						serviceNamespace = strVal;
						continue;
				}
			}

			if (JaegerTagTransformer.Instance.TryTransformTag(label, out var result))
			{
				Tags ??= new Dictionary<string, JaegerTag>();

				Tags[key] = result;
			}
		}

		if (!string.IsNullOrWhiteSpace(serviceName)) {
			serviceName = string.IsNullOrEmpty(serviceNamespace)
				? serviceName
				: serviceNamespace + "." + serviceName;
		} else {
			serviceName = (string)provider.GetDefaultResource().Attributes.FirstOrDefault(
				pair => pair.Key == ResourceSemanticConventions.AttributeServiceName).Value;
		}

		ServiceName = serviceName;
	}
}

internal static class ResourceSemanticConventions
{
	public const string AttributeServiceName = "service.name";
	public const string AttributeServiceNamespace = "service.namespace";
	public const string AttributeServiceInstance = "service.instance.id";
	public const string AttributeServiceVersion = "service.version";

	public const string AttributeTelemetrySdkName = "telemetry.sdk.name";
	public const string AttributeTelemetrySdkLanguage = "telemetry.sdk.language";
	public const string AttributeTelemetrySdkVersion = "telemetry.sdk.version";

	public const string AttributeContainerName = "container.name";
	public const string AttributeContainerImage = "container.image.name";
	public const string AttributeContainerTag = "container.image.tag";

	public const string AttributeFaasName = "faas.name";
	public const string AttributeFaasId = "faas.id";
	public const string AttributeFaasVersion = "faas.version";
	public const string AttributeFaasInstance = "faas.instance";

	public const string AttributeK8sCluster = "k8s.cluster.name";
	public const string AttributeK8sNamespace = "k8s.namespace.name";
	public const string AttributeK8sPod = "k8s.pod.name";
	public const string AttributeK8sDeployment = "k8s.deployment.name";

	public const string AttributeHostHostname = "host.hostname";
	public const string AttributeHostId = "host.id";
	public const string AttributeHostName = "host.name";
	public const string AttributeHostType = "host.type";
	public const string AttributeHostImageName = "host.image.name";
	public const string AttributeHostImageId = "host.image.id";
	public const string AttributeHostImageVersion = "host.image.version";

	public const string AttributeProcessId = "process.id";
	public const string AttributeProcessExecutableName = "process.executable.name";
	public const string AttributeProcessExecutablePath = "process.executable.path";
	public const string AttributeProcessCommand = "process.command";
	public const string AttributeProcessCommandLine = "process.command_line";
	public const string AttributeProcessUsername = "process.username";

	public const string AttributeCloudProvider = "cloud.provider";
	public const string AttributeCloudAccount = "cloud.account.id";
	public const string AttributeCloudRegion = "cloud.region";
	public const string AttributeCloudZone = "cloud.zone";
	public const string AttributeComponent = "component";
}