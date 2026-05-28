namespace ReadingQueue.Domain.Entities;

public class Subgenre
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    // The application stores genres as text (Name). Keep a reference to the genre name.
    public string Genre { get; set; } = null!;
}