using System.Text;
using System.Text.Json;
using WebApplication1.OTel;

internal readonly struct JaegerLog
{
	public JaegerLog(long timestamp, in PooledList<JaegerTag> fields)
	{
		Timestamp = timestamp;
		Fields = fields;
	}

	public long Timestamp { get; }

	public PooledList<JaegerTag> Fields { get; }

	public void Write(Utf8JsonWriter writer)
	{
		writer.WriteNumber("timestamp", Timestamp);
		writer.WriteStartArray("fields");
		foreach (var field in Fields) {
			writer.WriteStartObject();
			field.Write(writer);
			writer.WriteEndObject();
		}
		writer.WriteEndArray();
	}

	public override string ToString()
	{
		var sb = new StringBuilder("Log(");
		sb.Append(", Timestamp: ");
		sb.Append(Timestamp);
		sb.Append(", Fields: ");
		sb.Append(Fields);
		sb.Append(')');
		return sb.ToString();
	}
}