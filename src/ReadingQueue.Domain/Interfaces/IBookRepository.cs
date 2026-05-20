using System.Data;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Interfaces;

public interface IBookRepository
{
    Task<IEnumerable<Book>> GetByUserAsync(
        int userId, BookFilter filter, CancellationToken ct = default);

    Task<Book?> GetByIdAsync(
        int bookId, int userId, CancellationToken ct = default);

    Task<int> CreateAsync(
        int userId, CreateBookData data, CancellationToken ct = default);

    Task UpdateAsync(
        int bookId, int userId, UpdateBookData data, CancellationToken ct = default);

    Task DeleteAsync(
        int bookId, int userId, IDbTransaction tx, CancellationToken ct = default);

    Task DeleteFromQueueAsync(
        int bookId, int userId, IDbTransaction tx, CancellationToken ct = default);

    Task DeleteFromSuggestionsAsync(
        int bookId, int userId, IDbTransaction tx, CancellationToken ct = default);

    Task MarkAsReadAsync(
        int bookId, int userId, DateTime readAt, string? notes,
        IDbTransaction tx, CancellationToken ct = default);

    Task RemoveFromQueueIfPresentAsync(
        int bookId, int userId, IDbTransaction tx, CancellationToken ct = default);

    Task MarkAsUnreadAsync(
        int bookId, int userId, CancellationToken ct = default);

    Task<IEnumerable<Book>> GetUnreadByUserAsync(
        int userId, CancellationToken ct = default);

    Task<IEnumerable<Book>> GetReadByUserAsync(
        int userId, CancellationToken ct = default);
}
