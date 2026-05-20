namespace ReadingQueue.Domain.Entities;

public sealed class AISuggestion
{
    public int      Id          { get; }
    public int      UserId      { get; }
    public int      BookId      { get; }
    public string   Reasoning   { get; }
    public decimal  Score       { get; }
    public DateTime GeneratedAt { get; }
    public bool?    WasAccepted { get; }
    public string   BookTitle   { get; set; } = string.Empty; // poblado por el repositorio via JOIN

    public AISuggestion(int id, int userId, int bookId, string reasoning,
                        decimal score, DateTime generatedAt, bool? wasAccepted)
    {
        Id          = id;
        UserId      = userId;
        BookId      = bookId;
        Reasoning   = reasoning;
        Score       = score;
        GeneratedAt = generatedAt;
        WasAccepted = wasAccepted;
    }
}
