using System.Data;
using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlQueueRepository : IQueueRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlQueueRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<QueueItem>> GetByUserAsync(
        int userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<dynamic>(QueueQueries.GetByUser, new { UserId = userId });
        return rows.Select(r => new QueueItem
        {
            Id       = (int)r.Id,
            UserId   = (int)r.UserId,
            BookId   = (int)r.BookId,
            Position = (int)r.Position,
            AddedAt  = (DateTime)r.AddedAt,
            Source   = (string)r.Source,
            Book     = new Book
            {
                Id               = (int)r.BookId2,
                UserId           = (int)r.UserId2,
                Title            = (string)r.Title,
                Author           = (string)r.Author,
                Genre            = (string)r.Genre,
                Country          = (string)r.Country,
                WhyRead          = (string?)r.WhyRead,
                Priority         = (int)(byte)r.Priority,
                MentalEnergy     = (string)r.MentalEnergy,
                RecommendedMood  = (string)r.RecommendedMood,
                RotationCategory = (string)r.RotationCategory,
                IsRead           = (bool)r.IsRead,
                ReadAt           = (DateTime?)r.ReadAt,
                Notes            = (string?)r.Notes,
                CreatedAt        = (DateTime)r.BookCreatedAt,
                UpdatedAt        = (DateTime)r.BookUpdatedAt
            }
        }).ToList();
    }

    public async Task ReplaceQueueAsync(
        int userId,
        IEnumerable<(int bookId, int position, string source)> items,
        IDbTransaction tx,
        CancellationToken ct = default)
    {
        var conn = tx.Connection!;
        await conn.ExecuteAsync(QueueQueries.DeleteByUser, new { UserId = userId }, tx);
        foreach (var (bookId, position, source) in items)
            await conn.ExecuteAsync(QueueQueries.Insert,
                new { UserId = userId, BookId = bookId, Position = position, Source = source }, tx);
    }

    public async Task UpdatePositionsAsync(
        int userId,
        IEnumerable<(int bookId, int position)> positions,
        IDbTransaction tx,
        CancellationToken ct = default)
    {
        var conn = tx.Connection!;
        foreach (var (bookId, position) in positions)
            await conn.ExecuteAsync(QueueQueries.UpdatePosition,
                new { UserId = userId, BookId = bookId, Position = position }, tx);
    }

    public async Task RemoveItemAsync(
        int userId, int bookId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(QueueQueries.DeleteByBook, new { UserId = userId, BookId = bookId });
    }

    public async Task<bool> ContainsBookAsync(
        int userId, int bookId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var count = await conn.ExecuteScalarAsync<int>(
            QueueQueries.ExistsBook, new { UserId = userId, BookId = bookId });
        return count > 0;
    }
}
