
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal readonly struct Int128
{
	public static Int128 Empty;

	private const int SpanIdBytes = 8;
	private const int TraceIdBytes = 16;

	public Int128(ActivitySpanId spanId)
	{
		Span<byte> bytes = stackalloc byte[SpanIdBytes];
		spanId.CopyTo(bytes);

		if (BitConverter.IsLittleEndian)
		{
			bytes.Reverse();
		}

		var longs = MemoryMarshal.Cast<byte, long>(bytes);
		High = 0;
		Low = longs[0];
	}

	public Int128(ActivityTraceId traceId)
	{
		Span<byte> bytes = stackalloc byte[TraceIdBytes];
		traceId.CopyTo(bytes);

		if (BitConverter.IsLittleEndian)
		{
			bytes.Reverse();
		}

		var longs = MemoryMarshal.Cast<byte, long>(bytes);
		High = BitConverter.IsLittleEndian ? longs[1] : longs[0];
		Low = BitConverter.IsLittleEndian ? longs[0] : longs[1];
	}

	public long High { get; }

	public long Low { get; }
}