using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;

namespace MiniErp.Tests;

/// <summary>
/// Crea un AppDbContext sobre SQLite en memoria. La conexion se mantiene abierta
/// mientras viva el contexto (SQLite borra la base in-memory al cerrar la conexion).
///
/// NO MODIFICAR.
/// </summary>
public static class TestDbFactory
{
    public static AppDbContext Create()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
