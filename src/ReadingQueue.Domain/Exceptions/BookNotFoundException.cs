namespace ReadingQueue.Domain.Exceptions;

public sealed class BookNotFoundException : Exception
{
    public BookNotFoundException(int bookId)
        : base($"Libro {bookId} no encontrado.") { }
}
