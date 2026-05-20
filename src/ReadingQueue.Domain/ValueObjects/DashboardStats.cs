using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.ValueObjects;

public sealed record DashboardStats(
    int    TotalBooks,
    int    ReadBooks,
    int    UnreadBooks,
    double ReadPercentage,
    IReadOnlyList<GenreStat>        ByGenre,
    IReadOnlyList<RotationStat>     ByRotationCategory,
    IReadOnlyList<MentalEnergyStat> ByMentalEnergy,
    IReadOnlyList<CountryStat>      ByCountry,
    IReadOnlyList<Book>             TopUnreadPriority,
    IReadOnlyList<Book>             RecentlyRead
);

public sealed record GenreStat(string Genre, int Total, int Read, int Unread);
public sealed record RotationStat(string Category, int Total, int Read, int Unread);
public sealed record MentalEnergyStat(string Level, int Total, int Unread);
public sealed record CountryStat(string Country, int Total);
