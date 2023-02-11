using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Trace;

namespace WebApplication1.OTel;
internal static class StatusHelper
{
	public const string UnsetStatusCodeTagValue = "UNSET";
	public const string OkStatusCodeTagValue = "OK";
	public const string ErrorStatusCodeTagValue = "ERROR";

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string GetTagValueForStatusCode(StatusCode statusCode)
	{
		return statusCode switch
		{
			/*
			 * Note: Order here does matter for perf. Unset is
			 * first because assumption is most spans will be
			 * Unset, then Error. Ok is not set by the SDK.
			 */
			StatusCode.Unset => UnsetStatusCodeTagValue,
			StatusCode.Error => ErrorStatusCodeTagValue,
			StatusCode.Ok => OkStatusCodeTagValue,
			_ => null,
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static StatusCode? GetStatusCodeForTagValue(string statusCodeTagValue)
	{
		return statusCodeTagValue switch
		{
			/*
			 * Note: Order here does matter for perf. Unset is
			 * first because assumption is most spans will be
			 * Unset, then Error. Ok is not set by the SDK.
			 */
			string _ when UnsetStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => StatusCode.Unset,
			string _ when ErrorStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => StatusCode.Error,
			string _ when OkStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => StatusCode.Ok,
			_ => null,
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetStatusCodeForTagValue(string statusCodeTagValue, out StatusCode statusCode)
	{
		StatusCode? tempStatusCode = GetStatusCodeForTagValue(statusCodeTagValue);

		statusCode = tempStatusCode ?? default;

		return tempStatusCode.HasValue;
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

internal readonly struct JaegerTag
	{
		public JaegerTag(
			string key,
			JaegerTagType vType,
			string vStr = null,
			double? vDouble = null,
			bool? vBool = null,
			long? vLong = null,
			byte[] vBinary = null)
		{
			Key = key;
			VType = vType;

			VStr = vStr;
			VDouble = vDouble;
			VBool = vBool;
			VLong = vLong;
			VBinary = vBinary;
		}

		public string Key { get; }

		public JaegerTagType VType { get; }

		public string VStr { get; }

		public double? VDouble { get; }

		public bool? VBool { get; }

		public long? VLong { get; }

		public byte[] VBinary { get; }


		public void Write(Utf8JsonWriter writer) {
			writer.WriteString("key", Key);
			writer.WriteString("type", VType.ToString().ToLower());
			if (VBool.HasValue) {
				writer.WriteBoolean("value", VBool.Value);
			} else if (VBinary != null) {
				writer.WriteBase64String("value", VBinary);
			} if (VStr != null) {
				writer.WriteString("value", VStr);
			} else if (VDouble.HasValue) {
				writer.WriteNumber("value", VDouble.Value);
			} else if (VLong.HasValue) {
				writer.WriteNumber("value", VLong.Value);
			}
		}

		public override string ToString()
		{
			var sb = new StringBuilder("Tag(");
			sb.Append(", Key: ");
			sb.Append(Key);
			sb.Append(", VType: ");
			sb.Append(VType);
			if (VStr != null)
			{
				sb.Append(", VStr: ");
				sb.Append(VStr);
			}

			if (VDouble.HasValue)
			{
				sb.Append(", VDouble: ");
				sb.Append(VDouble);
			}

			if (VBool.HasValue)
			{
				sb.Append(", VBool: ");
				sb.Append(VBool);
			}

			if (VLong.HasValue)
			{
				sb.Append(", VLong: ");
				sb.Append(VLong);
			}

			if (VBinary != null)
			{
				sb.Append(", VBinary: ");
				sb.Append(VBinary);
			}

			sb.Append(')');
			return sb.ToString();
		}
	}

internal abstract class TagTransformer<T>
{
	public bool TryTransformTag(KeyValuePair<string, object> tag, out T result, int? maxLength = null)
	{
		if (tag.Value == null)
		{
			result = default;
			return false;
		}

		switch (tag.Value)
		{
			case char:
			case string:
				result = TransformStringTag(tag.Key, TruncateString(Convert.ToString(tag.Value), maxLength));
				break;
			case bool b:
				result = TransformBooleanTag(tag.Key, b);
				break;
			case byte:
			case sbyte:
			case short:
			case ushort:
			case int:
			case uint:
			case long:
				result = TransformIntegralTag(tag.Key, Convert.ToInt64(tag.Value));
				break;
			case float:
			case double:
				result = TransformFloatingPointTag(tag.Key, Convert.ToDouble(tag.Value));
				break;
			case Array array:
				try
				{
					result = TransformArrayTagInternal(tag.Key, array, maxLength);
				}
				catch
				{
					// If an exception is thrown when calling ToString
					// on any element of the array, then the entire array value
					// is ignored.
					result = default;
					return false;
				}

				break;

			// All other types are converted to strings including the following
			// built-in value types:
			// case nint:    Pointer type.
			// case nuint:   Pointer type.
			// case ulong:   May throw an exception on overflow.
			// case decimal: Converting to double produces rounding errors.
			default:
				try
				{
					result = TransformStringTag(tag.Key, TruncateString(tag.Value.ToString(), maxLength));
				}
				catch
				{
					// If ToString throws an exception then the tag is ignored.
					result = default;
					return false;
				}

				break;
		}

		return true;
	}

	protected abstract T TransformIntegralTag(string key, long value);

	protected abstract T TransformFloatingPointTag(string key, double value);

	protected abstract T TransformBooleanTag(string key, bool value);

	protected abstract T TransformStringTag(string key, string value);

	protected abstract T TransformArrayTag(string key, Array array);

	private static string TruncateString(string value, int? maxLength)
	{
		return maxLength.HasValue && value?.Length > maxLength
			? value.Substring(0, maxLength.Value)
			: value;
	}

	private T TransformArrayTagInternal(string key, Array array, int? maxStringValueLength)
	{
		// This switch ensures the values of the resultant array-valued tag are of the same type.
		return array switch
		{
			char[] => TransformArrayTag(key, array),
			string[] => ConvertToStringArrayThenTransformArrayTag(key, array, maxStringValueLength),
			bool[] => TransformArrayTag(key, array),
			byte[] => TransformArrayTag(key, array),
			sbyte[] => TransformArrayTag(key, array),
			short[] => TransformArrayTag(key, array),
			ushort[] => TransformArrayTag(key, array),
			int[] => TransformArrayTag(key, array),
			uint[] => TransformArrayTag(key, array),
			long[] => TransformArrayTag(key, array),
			float[] => TransformArrayTag(key, array),
			double[] => TransformArrayTag(key, array),
			_ => ConvertToStringArrayThenTransformArrayTag(key, array, maxStringValueLength),
		};
	}

	private T ConvertToStringArrayThenTransformArrayTag(string key, Array array, int? maxStringValueLength)
	{
		string[] stringArray;

		if (array is string[] arrayAsStringArray && (!maxStringValueLength.HasValue || !arrayAsStringArray.Any(s => s?.Length > maxStringValueLength)))
		{
			stringArray = arrayAsStringArray;
		}
		else
		{
			stringArray = new string[array.Length];
			for (var i = 0; i < array.Length; ++i)
			{
				stringArray[i] = TruncateString(array.GetValue(i)?.ToString(), maxStringValueLength);
			}
		}

		return TransformArrayTag(key, stringArray);
	}
}

internal sealed class JaegerTagTransformer : TagTransformer<JaegerTag>
{
	private JaegerTagTransformer()
	{
	}

	public static JaegerTagTransformer Instance { get; } = new();

	protected override JaegerTag TransformIntegralTag(string key, long value)
	{
		return new JaegerTag(key, JaegerTagType.LONG, vLong: value);
	}

	protected override JaegerTag TransformFloatingPointTag(string key, double value)
	{
		return new JaegerTag(key, JaegerTagType.DOUBLE, vDouble: value);
	}

	protected override JaegerTag TransformBooleanTag(string key, bool value)
	{
		return new JaegerTag(key, JaegerTagType.BOOL, vBool: value);
	}

	protected override JaegerTag TransformStringTag(string key, string value)
	{
		return new JaegerTag(key, JaegerTagType.STRING, vStr: value);
	}

	protected override JaegerTag TransformArrayTag(string key, Array array)
		=> TransformStringTag(key, JsonSerializer.Serialize(array));
}

struct TagEnumerationState : PeerServiceResolver.IPeerServiceState
{
	public TagEnumerationState(List<JaegerTag> tags) {
		Tags = Tags;
	}
	public List<JaegerTag> Tags;

	public string PeerService { get; set; }

	public int? PeerServicePriority { get; set; }

	public string HostName { get; set; }

	public string IpAddress { get; set; }

	public long Port { get; set; }

	public StatusCode? StatusCode { get; set; }

	public string StatusDescription { get; set; }

	public void EnumerateTags(Activity activity)
	{
		foreach (ref readonly var tag in activity.EnumerateTagObjects())
		{
			if (tag.Value != null)
			{
				var key = tag.Key;

				if (!JaegerTagTransformer.Instance.TryTransformTag(tag, out var jaegerTag))
				{
					continue;
				}

				if (jaegerTag.VStr != null)
				{
					PeerServiceResolver.InspectTag(ref this, key, jaegerTag.VStr);

					if (key == SpanAttributeConstants.StatusCodeKey)
					{
						StatusCode? statusCode = StatusHelper.GetStatusCodeForTagValue(jaegerTag.VStr);
						StatusCode = statusCode;
						continue;
					}
					else if (key == SpanAttributeConstants.StatusDescriptionKey)
					{
						StatusDescription = jaegerTag.VStr;
						continue;
					}
				}
				else if (jaegerTag.VLong.HasValue)
				{
					PeerServiceResolver.InspectTag(ref this, key, jaegerTag.VLong.Value);
				}

				Tags.Add(jaegerTag);
			}
		}
	}
}
internal static class PeerServiceResolver
	{
		private static readonly Dictionary<string, int> PeerServiceKeyResolutionDictionary = new(StringComparer.OrdinalIgnoreCase)
		{
			[SemanticConventions.AttributePeerService] = 0, // priority 0 (highest).
			["peer.hostname"] = 1,
			["peer.address"] = 1,
			[SemanticConventions.AttributeHttpHost] = 2, // peer.service for Http.
			[SemanticConventions.AttributeDbInstance] = 2, // peer.service for Redis.
		};

		public interface IPeerServiceState
		{
			string PeerService { get; set; }

			int? PeerServicePriority { get; set; }

			string HostName { get; set; }

			string IpAddress { get; set; }

			long Port { get; set; }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InspectTag<T>(ref T state, string key, string value)
			where T : struct, IPeerServiceState
		{
			if (PeerServiceKeyResolutionDictionary.TryGetValue(key, out int priority)
				&& (state.PeerService == null || priority < state.PeerServicePriority))
			{
				state.PeerService = value;
				state.PeerServicePriority = priority;
			}
			else if (key == SemanticConventions.AttributeNetPeerName)
			{
				state.HostName = value;
			}
			else if (key == SemanticConventions.AttributeNetPeerIp)
			{
				state.IpAddress = value;
			}
			else if (key == SemanticConventions.AttributeNetPeerPort && long.TryParse(value, out var port))
			{
				state.Port = port;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void InspectTag<T>(ref T state, string key, long value)
			where T : struct, IPeerServiceState
		{
			if (key == SemanticConventions.AttributeNetPeerPort)
			{
				state.Port = value;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Resolve<T>(ref T state, out string peerServiceName, out bool addAsTag)
			where T : struct, IPeerServiceState
		{
			peerServiceName = state.PeerService;

			// If priority = 0 that means peer.service was included in tags
			addAsTag = state.PeerServicePriority != 0;

			if (addAsTag)
			{
				var hostNameOrIpAddress = state.HostName ?? state.IpAddress;

				// peer.service has not already been included, but net.peer.name/ip and optionally net.peer.port are present
				if (hostNameOrIpAddress != null)
				{
					peerServiceName = state.Port == default
						? hostNameOrIpAddress
						: $"{hostNameOrIpAddress}:{state.Port}";
				}
				else if (state.PeerService != null)
				{
					peerServiceName = state.PeerService;
				}
			}
		}
	}
	internal static class SemanticConventions
	{
		// The set of constants matches the specification as of this commit.
		// https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/trace/semantic_conventions
		// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md
		public const string AttributeNetTransport = "net.transport";
		public const string AttributeNetPeerIp = "net.peer.ip";
		public const string AttributeNetPeerPort = "net.peer.port";
		public const string AttributeNetPeerName = "net.peer.name";
		public const string AttributeNetHostIp = "net.host.ip";
		public const string AttributeNetHostPort = "net.host.port";
		public const string AttributeNetHostName = "net.host.name";

		public const string AttributeEnduserId = "enduser.id";
		public const string AttributeEnduserRole = "enduser.role";
		public const string AttributeEnduserScope = "enduser.scope";

		public const string AttributePeerService = "peer.service";

		public const string AttributeHttpMethod = "http.method";
		public const string AttributeHttpUrl = "http.url";
		public const string AttributeHttpTarget = "http.target";
		public const string AttributeHttpHost = "http.host";
		public const string AttributeHttpScheme = "http.scheme";
		public const string AttributeHttpStatusCode = "http.status_code";
		public const string AttributeHttpStatusText = "http.status_text";
		public const string AttributeHttpFlavor = "http.flavor";
		public const string AttributeHttpServerName = "http.server_name";
		public const string AttributeHttpRoute = "http.route";
		public const string AttributeHttpClientIP = "http.client_ip";
		public const string AttributeHttpUserAgent = "http.user_agent";
		public const string AttributeHttpRequestContentLength = "http.request_content_length";
		public const string AttributeHttpRequestContentLengthUncompressed = "http.request_content_length_uncompressed";
		public const string AttributeHttpResponseContentLength = "http.response_content_length";
		public const string AttributeHttpResponseContentLengthUncompressed = "http.response_content_length_uncompressed";

		public const string AttributeDbSystem = "db.system";
		public const string AttributeDbConnectionString = "db.connection_string";
		public const string AttributeDbUser = "db.user";
		public const string AttributeDbMsSqlInstanceName = "db.mssql.instance_name";
		public const string AttributeDbJdbcDriverClassName = "db.jdbc.driver_classname";
		public const string AttributeDbName = "db.name";
		public const string AttributeDbStatement = "db.statement";
		public const string AttributeDbOperation = "db.operation";
		public const string AttributeDbInstance = "db.instance";
		public const string AttributeDbUrl = "db.url";
		public const string AttributeDbCassandraKeyspace = "db.cassandra.keyspace";
		public const string AttributeDbHBaseNamespace = "db.hbase.namespace";
		public const string AttributeDbRedisDatabaseIndex = "db.redis.database_index";
		public const string AttributeDbMongoDbCollection = "db.mongodb.collection";

		public const string AttributeRpcSystem = "rpc.system";
		public const string AttributeRpcService = "rpc.service";
		public const string AttributeRpcMethod = "rpc.method";
		public const string AttributeRpcGrpcStatusCode = "rpc.grpc.status_code";

		public const string AttributeMessageType = "message.type";
		public const string AttributeMessageId = "message.id";
		public const string AttributeMessageCompressedSize = "message.compressed_size";
		public const string AttributeMessageUncompressedSize = "message.uncompressed_size";

		public const string AttributeFaasTrigger = "faas.trigger";
		public const string AttributeFaasExecution = "faas.execution";
		public const string AttributeFaasDocumentCollection = "faas.document.collection";
		public const string AttributeFaasDocumentOperation = "faas.document.operation";
		public const string AttributeFaasDocumentTime = "faas.document.time";
		public const string AttributeFaasDocumentName = "faas.document.name";
		public const string AttributeFaasTime = "faas.time";
		public const string AttributeFaasCron = "faas.cron";

		public const string AttributeMessagingSystem = "messaging.system";
		public const string AttributeMessagingDestination = "messaging.destination";
		public const string AttributeMessagingDestinationKind = "messaging.destination_kind";
		public const string AttributeMessagingTempDestination = "messaging.temp_destination";
		public const string AttributeMessagingProtocol = "messaging.protocol";
		public const string AttributeMessagingProtocolVersion = "messaging.protocol_version";
		public const string AttributeMessagingUrl = "messaging.url";
		public const string AttributeMessagingMessageId = "messaging.message_id";
		public const string AttributeMessagingConversationId = "messaging.conversation_id";
		public const string AttributeMessagingPayloadSize = "messaging.message_payload_size_bytes";
		public const string AttributeMessagingPayloadCompressedSize = "messaging.message_payload_compressed_size_bytes";
		public const string AttributeMessagingOperation = "messaging.operation";

		public const string AttributeExceptionEventName = "exception";
		public const string AttributeExceptionType = "exception.type";
		public const string AttributeExceptionMessage = "exception.message";
		public const string AttributeExceptionStacktrace = "exception.stacktrace";
	}