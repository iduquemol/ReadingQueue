namespace ReadingQueue.Domain.Exceptions;

public sealed class DailyLimitExceededException : Exception
{
    public DailyLimitExceededException(string message) : base(message) { }
}
