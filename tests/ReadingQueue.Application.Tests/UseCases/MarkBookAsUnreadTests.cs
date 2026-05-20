using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class MarkBookAsUnreadTests
{
    private readonly Mock<IBookRepository> _books = new();
    private readonly MarkBookAsUnread _sut;

    public MarkBookAsUnreadTests() => _sut = new MarkBookAsUnread(_books.Object);

    private static Book MakeBook(bool isRead = true)
        => new(7, 42, "Titulo", "Autor", "Clasico", "Colombia", null,
               3, "Baja - cualquier momento", "Analitico / quiero aprender algo",
               "Clasico", isRead, isRead ? DateTime.UtcNow : null, null,
               DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task ExecuteAsync_BookNotFound_ThrowsBookNotFoundException()
    {
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync((Book?)null);

        await _sut.Invoking(s => s.ExecuteAsync(new MarkBookAsUnread.Command(7, 42)))
                  .Should().ThrowAsync<BookNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_BookExists_CallsMarkAsUnreadAsync()
    {
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(MakeBook());

        await _sut.ExecuteAsync(new MarkBookAsUnread.Command(7, 42));

        _books.Verify(r => r.MarkAsUnreadAsync(7, 42, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyUnread_IsIdempotent()
    {
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(MakeBook(isRead: false));

        var act = () => _sut.ExecuteAsync(new MarkBookAsUnread.Command(7, 42));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBookWithIsReadFalse()
    {
        var unread = MakeBook(isRead: false);
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(unread);

        var result = await _sut.ExecuteAsync(new MarkBookAsUnread.Command(7, 42));

        result.IsRead.Should().BeFalse();
    }
}
