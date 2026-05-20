namespace ReadingQueue.Api.Responses;

public sealed record GenerateQueueResponse(
    bool                             AiContributed,
    IReadOnlyList<QueueItemWithAIResponse> Queue
);
