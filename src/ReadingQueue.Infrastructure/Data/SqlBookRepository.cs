using System.Data;
using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlBookRepository : IBookRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlBookRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<Book>> GetByUserAsync(
        int userId, BookFilter filter, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Book>(BookQueries.GetByUserFiltered, new
        {
            UserId       = userId,
            Genre        = filter.Genre,
            Country      = filter.Country,
            MentalEnergy = filter.MentalEnergy,
            Mood         = filter.Mood,
            Rotation     = filter.Rotation,
            MinPriority  = filter.MinPriority,
            IsRead       = filter.IsRead.HasValue ? (object)(filter.IsRead.Value ? 1 : 0) : null,
            SearchQuery  = filter.SearchQuery
        });
    }

    public async Task<Book?> GetByIdAsync(int bookId, int userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Book>(
            BookQueries.GetById, new { BookId = bookId, UserId = userId });
    }

    public async Task<int> CreateAsync(int userId, CreateBookData data, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(BookQueries.Insert, new
        {
            UserId           = userId,
            data.Title,
            data.Author,
            data.Genre,
            Subgenre         = data.Subgenre,
            data.Country,
            data.WhyRead,
            data.Priority,
            data.MentalEnergy,
            data.RecommendedMood,
            data.RotationCategory,
            data.Notes
        });
    }

    public async Task UpdateAsync(int bookId, int userId, UpdateBookData data, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(BookQueries.Update, new
        {
            BookId           = bookId,
            UserId           = userId,
            data.Title,
            data.Author,
            data.Genre,
            Subgenre         = data.Subgenre,
            data.Country,
            data.WhyRead,
            data.Priority,
            data.MentalEnergy,
            data.RecommendedMood,
            data.RotationCategory,
            data.Notes
        });
    }

    public async Task DeleteAsync(int bookId, int userId, IDbTransaction tx, CancellationToken ct = default)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.Delete, new { BookId = bookId, UserId = userId }, tx);

    public async Task DeleteFromQueueAsync(int bookId, int userId, IDbTransaction tx, CancellationToken ct = default)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.DeleteFromQueue, new { BookId = bookId, UserId = userId }, tx);

    public async Task DeleteFromSuggestionsAsync(int bookId, int userId, IDbTransaction tx, CancellationToken ct = default)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.DeleteFromSuggestions, new { BookId = bookId, UserId = userId }, tx);

    public async Task MarkAsReadAsync(int bookId, int userId, DateTime readAt,
        string? notes, IDbTransaction tx, CancellationToken ct = default)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.MarkAsRead,
            new { BookId = bookId, UserId = userId, ReadAt = readAt, Notes = notes }, tx);

    public async Task RemoveFromQueueIfPresentAsync(int bookId, int userId,
        IDbTransaction tx, CancellationToken ct = default)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.RemoveFromQueueIfPresent,
            new { BookId = bookId, UserId = userId }, tx);

    public async Task MarkAsUnreadAsync(int bookId, int userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(BookQueries.MarkAsUnread, new { BookId = bookId, UserId = userId });
    }

    public async Task<IEnumerable<Book>> GetUnreadByUserAsync(int userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Book>(BookQueries.GetUnreadByUser, new { UserId = userId });
    }

    public async Task<IEnumerable<Book>> GetReadByUserAsync(int userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Book>(BookQueries.GetReadByUser, new { UserId = userId });
    }
}
