namespace ReadingQueue.Api.Requests;

public sealed record ReorderQueueRequest(
    IReadOnlyList<QueuePositionItem> Positions);

public sealed record QueuePositionItem(int BookId, int Position);
