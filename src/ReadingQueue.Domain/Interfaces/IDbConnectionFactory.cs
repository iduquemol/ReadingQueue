using System.Data;

namespace ReadingQueue.Domain.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
