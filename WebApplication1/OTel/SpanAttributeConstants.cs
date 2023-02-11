namespace WebApplication1.OTel;

internal static class SpanAttributeConstants
{
	public const string StatusCodeKey = "otel.status_code";
	public const string StatusDescriptionKey = "otel.status_description";
	public const string DatabaseStatementTypeKey = "db.statement_type";
}