namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    /// <summary>
    /// Represents the different types of Jaeger Spans.
    /// </summary>
    internal enum JaegerSpanRefType
    {
        /// <summary>
        /// A child span.
        /// </summary>
        CHILD_OF = 0,

        /// <summary>
        /// A sibling span.
        /// </summary>
        FOLLOWS_FROM = 1,
    }
}
