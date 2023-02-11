using System.Text;
using System.Text.Json;
using OpenTelemetry.Exporter.Jaeger.Implementation;

namespace WebApplication1.OTel;

internal readonly struct JaegerSpanRef 
{
        public JaegerSpanRef(JaegerSpanRefType refType, long traceIdLow, long traceIdHigh, long spanId)
        {
            RefType = refType;
            TraceIdLow = traceIdLow;
            TraceIdHigh = traceIdHigh;
            SpanId = spanId;
        }

        public JaegerSpanRefType RefType { get; }

        public long TraceIdLow { get; }

        public long TraceIdHigh { get; }

        public long SpanId { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteString("refType", RefType.ToString());
            writer.WriteString("traceID", TraceIdLow.ToString("x"));
            writer.WriteString("spanID", SpanId.ToString("x"));
        }

        /// <summary>
        /// <seealso cref="JaegerSpanRefType"/>
        /// </summary>
        /// <returns>A string representation of the object.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder("SpanRef(");
            sb.Append(", RefType: ");
            sb.Append(RefType);
            sb.Append(", TraceIdLow: ");
            sb.Append(TraceIdLow);
            sb.Append(", TraceIdHigh: ");
            sb.Append(TraceIdHigh);
            sb.Append(", SpanId: ");
            sb.Append(SpanId);
            sb.Append(')');
            return sb.ToString();
        }
    }