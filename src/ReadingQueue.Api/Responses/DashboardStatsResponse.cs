namespace ReadingQueue.Api.Responses;

public sealed record DashboardStatsResponse(
    int    TotalBooks,
    int    ReadBooks,
    int    UnreadBooks,
    double ReadPercentage,
    IReadOnlyList<GenreStatResponse>        ByGenre,
    IReadOnlyList<RotationStatResponse>     ByRotationCategory,
    IReadOnlyList<MentalEnergyStatResponse> ByMentalEnergy,
    IReadOnlyList<CountryStatResponse>      ByCountry,
    IReadOnlyList<BookResponse>             TopUnreadPriority,
    IReadOnlyList<BookResponse>             RecentlyRead
);

public sealed record GenreStatResponse(string Genre, int Total, int Read, int Unread);
public sealed record RotationStatResponse(string Category, int Total, int Read, int Unread);
public sealed record MentalEnergyStatResponse(string Level, int Total, int Unread);
public sealed record CountryStatResponse(string Country, int Total);
