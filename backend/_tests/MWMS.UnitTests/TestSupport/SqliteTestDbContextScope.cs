using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ModernWMS.Core.DBContext;

namespace ModernWMS.UnitTests.TestSupport
{
    /// <summary>
    /// Backs a real <see cref="SqlDBContext"/> with an open, in-memory SQLite connection.
    /// SqlDBContext has no interface and its entity model is built by reflecting over
    /// BaseModel subclasses, so tests use a real EF Core/SQLite provider instead of
    /// mocking DbContext/DbSet. The connection must stay open for the in-memory database
    /// to live for the scope's lifetime; disposing this scope tears it down.
    /// </summary>
    public sealed class SqliteTestDbContextScope : IDisposable
    {
        private readonly SqliteConnection _connection;

        public SqlDBContext DbContext { get; }

        public SqliteTestDbContextScope()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<SqlDBContext>()
                .UseSqlite(_connection)
                .Options;

            DbContext = new SqlDBContext(options);
            DbContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            DbContext.Dispose();
            _connection.Dispose();
        }
    }
}
