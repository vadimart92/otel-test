using System.Text;
using System.Text.Json;
using WebApplication1.OTel;

internal readonly struct JaegerSpan
    {
        public JaegerSpan(
            string peerServiceName,
            Int128 traceId,
            long spanId,
            long parentSpanId,
            string operationName,
            int flags,
            long startTime,
            long duration,
            in PooledList<JaegerSpanRef> references,
            in PooledList<JaegerTag> tags,
            in PooledList<JaegerLog> logs)
        {
            PeerServiceName = peerServiceName;
            TraceId = traceId;
            SpanId = spanId;
            ParentSpanId = parentSpanId;
            OperationName = operationName;
            Flags = flags;
            StartTime = startTime;
            Duration = duration;
            References = references;
            Tags = tags;
            Logs = logs;
        }

        public string PeerServiceName { get; }

        public Int128 TraceId { get; }

        public long SpanId { get; }

        public long ParentSpanId { get; }

        public string OperationName { get; }

        public PooledList<JaegerSpanRef> References { get; }

        public int Flags { get; }

        public long StartTime { get; }

        public long Duration { get; }

        public PooledList<JaegerTag> Tags { get; }

        public PooledList<JaegerLog> Logs { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteString("traceID", TraceId.Low.ToString("x"));
            writer.WriteString("spanID", SpanId.ToString("x"));
            writer.WriteNumber("flags", Flags);
            writer.WriteString("operationName", OperationName);
            writer.WriteString("parentSpanId", ParentSpanId.ToString("x"));
            writer.WriteNumber("startTime", StartTime);
            writer.WriteNumber("duration", Duration);
            writer.WriteStartArray("references");
            foreach (var reference in References) {
                writer.WriteStartObject();
                reference.Write(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("tags");
            foreach (var tag in Tags) {
                writer.WriteStartObject();
                tag.Write(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("logs");
            foreach (var log in Logs) {
                writer.WriteStartObject();
                log.Write(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
          /*
      "references": [{
        "refType": "CHILD_OF",
        "traceID": "665b3cc06e48f122",
        "spanID": "665b3cc06e48f122"
      }],
     
     
      "tags": [],
      "logs": [],
           * 
           */
        }

        public void Return()
        {
            References.Return();
            Tags.Return();
            if (!Logs.IsEmpty)
            {
                for (int i = 0; i < Logs.Count; i++)
                {
                    Logs[i].Fields.Return();
                }

                Logs.Return();
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder("Span(");
            sb.Append(", TraceIdLow: ");
            sb.Append(TraceId.Low);
            sb.Append(", TraceIdHigh: ");
            sb.Append(TraceId.High);
            sb.Append(", SpanId: ");
            sb.Append(SpanId);
            sb.Append(", ParentSpanId: ");
            sb.Append(ParentSpanId);
            sb.Append(", OperationName: ");
            sb.Append(OperationName);
            if (!References.IsEmpty)
            {
                sb.Append(", References: ");
                sb.Append(References);
            }

            sb.Append(", Flags: ");
            sb.Append(Flags);
            sb.Append(", StartTime: ");
            sb.Append(StartTime);
            sb.Append(", Duration: ");
            sb.Append(Duration);
            if (!Tags.IsEmpty)
            {
                sb.Append(", JaegerTags: ");
                sb.Append(Tags);
            }

            if (!Logs.IsEmpty)
            {
                sb.Append(", Logs: ");
                sb.Append(Logs);
            }

            sb.Append(')');
            return sb.ToString();
        }

    }