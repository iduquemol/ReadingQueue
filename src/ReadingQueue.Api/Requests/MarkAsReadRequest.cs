namespace ReadingQueue.Api.Requests;

public sealed record MarkAsReadRequest(DateTime? ReadAt, string? Notes);
