using FluentAssertions;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Tests;

public class BookEntityTests
{
    private static Book MakeBook(bool isRead = false, DateTime? readAt = null,
        string? whyRead = null, string? notes = null)
        => new(1, 42, "Cien años de soledad", "Gabriel Garcia Marquez",
               "Novela latinoamericana", null, "Colombia", whyRead,
               5, "Baja - cualquier momento", "Solemne / quiero leer algo grande",
               "Novela grande", isRead, readAt, notes,
               new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
               new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Constructor_AssignsAllProperties()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var readAt    = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        var book = new Book(7, 99, "Título", "Autor", "Clasico", "Subgénero", "España",
            "Porque sí", 4, "Alta - concentracion", "Analitico / quiero aprender algo",
            "Clasico", true, readAt, "Muy bueno", createdAt, updatedAt);

        book.Id.Should().Be(7);
        book.UserId.Should().Be(99);
        book.Title.Should().Be("Título");
        book.Author.Should().Be("Autor");
        book.Genre.Should().Be("Clasico");
        book.Subgenre.Should().Be("Subgénero");
        book.Country.Should().Be("España");
        book.WhyRead.Should().Be("Porque sí");
        book.Priority.Should().Be(4);
        book.MentalEnergy.Should().Be("Alta - concentracion");
        book.RecommendedMood.Should().Be("Analitico / quiero aprender algo");
        book.RotationCategory.Should().Be("Clasico");
        book.IsRead.Should().BeTrue();
        book.ReadAt.Should().Be(readAt);
        book.Notes.Should().Be("Muy bueno");
        book.CreatedAt.Should().Be(createdAt);
        book.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Constructor_WithIsReadFalse_AndReadAtNull_DoesNotThrow()
    {
        var act = () => MakeBook(isRead: false, readAt: null);

        act.Should().NotThrow();
        var book = act();
        book.IsRead.Should().BeFalse();
        book.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithIsReadTrue_AndReadAt_DoesNotThrow()
    {
        var readAt = DateTime.UtcNow;
        var act    = () => MakeBook(isRead: true, readAt: readAt);

        act.Should().NotThrow();
        var book = act();
        book.IsRead.Should().BeTrue();
        book.ReadAt.Should().Be(readAt);
    }

    [Fact]
    public void Constructor_WithNullWhyReadAndNotes_DoesNotThrow()
    {
        var act = () => MakeBook(whyRead: null, notes: null);

        act.Should().NotThrow();
        var book = act();
        book.WhyRead.Should().BeNull();
        book.Notes.Should().BeNull();
    }
}
