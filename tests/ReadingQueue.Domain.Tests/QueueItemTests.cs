using FluentAssertions;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Tests;

public class QueueItemTests
{
    private static Book MakeBook()
        => new(1, 42, "Cien años de soledad", "Gabriel Garcia Marquez",
               "Novela latinoamericana", "Colombia", null,
               5, "Baja - cualquier momento", "Solemne / quiero leer algo grande",
               "Novela grande", false, null, null,
               new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
               new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Constructor_AssignsAllProperties()
    {
        var book    = MakeBook();
        var addedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var item = new QueueItem(10, 42, 1, 3, addedAt, "Manual", book);

        item.Id.Should().Be(10);
        item.UserId.Should().Be(42);
        item.BookId.Should().Be(1);
        item.Position.Should().Be(3);
        item.AddedAt.Should().Be(addedAt);
        item.Source.Should().Be("Manual");
        item.Book.Should().BeSameAs(book);
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("AI")]
    [InlineData("Filter")]
    public void Constructor_AcceptsValidSources(string source)
    {
        var act = () => new QueueItem(1, 1, 1, 1,
            DateTime.UtcNow, source, MakeBook());

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_AssignsBook_Correctly()
    {
        var book = MakeBook();
        var item = new QueueItem(1, 1, 1, 1, DateTime.UtcNow, "AI", book);

        item.Book.Should().BeSameAs(book);
        item.Book.Title.Should().Be("Cien años de soledad");
    }
}
