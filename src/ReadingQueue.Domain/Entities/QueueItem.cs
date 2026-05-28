namespace ReadingQueue.Domain.Entities;

public sealed class QueueItem
{
    public int      Id       { get; init; }
    public int      UserId   { get; init; }
    public int      BookId   { get; init; }
    public int      Position { get; init; }
    public DateTime AddedAt  { get; init; }
    public string   Source   { get; init; } = null!;
    public string?  AiReasoning { get; init; }
    public Book     Book     { get; init; } = null!;

    public QueueItem() { }

    public QueueItem(int id, int userId, int bookId, int position,
                     DateTime addedAt, string source, Book book,
                     string? aiReasoning = null)
    {
        Id         = id;
        UserId     = userId;
        BookId     = bookId;
        Position   = position;
        AddedAt    = addedAt;
        Source     = source;
        AiReasoning = aiReasoning;
        Book       = book;
    }
}
