using System.Numerics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;

namespace LocaLink;


public static class Storage
{
    public static string dbName = "LocaLink.db";
    public static string dbFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, dbName);
    public static string connectionStr = $"Data Source={dbFilePath}";
    // public static SqliteConnection conn;

    public static void Init()
    {
        using (var conn = new SqliteConnection(connectionStr))
        {
            conn.Open();
            string sql = @"
                CREATE TABLE IF NOT EXISTS History (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Time INTEGER,
                    Message BLOB
                )
            ";
            var command = conn.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
            Console.WriteLine("Database connection successful!");
        }
    }
    
    public static void Add(WsMessage msg)
    {
        using (var conn = new SqliteConnection(connectionStr))
        {
            conn.Open();
            var command = conn.CreateCommand();
            command.CommandText = @"
                INSERT INTO History (Time, Message) VALUES (@time, @message)
            ";
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            command.Parameters.AddWithValue("@time", now);
            command.Parameters.AddWithValue("@message", JsonSerializer.Serialize(msg)); 
            command.ExecuteNonQuery();
        }
    }

    public static List<WsMessage> GetRecentsFromID(int id, int total = 10)
    {
        List<WsMessage> result = [];
        using (var conn = new SqliteConnection(connectionStr))
        {
            conn.Open();
            var command = conn.CreateCommand();
            command.CommandText = @"
                SELECT * FROM History WHERE Id <= @id ORDER BY Id DESC LIMIT @total
            ";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@total", total);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(JsonSerializer.Deserialize<WsMessage>(reader.GetString(2)));
                }
            }
        }
        return result;
    }

    public static int MaxID()
    {
        int result = -1;
        using (var conn = new SqliteConnection(connectionStr))
        {
            conn.Open();
            var command = conn.CreateCommand();
            command.CommandText = @"
                SELECT MAX(Id) FROM History
            ";
            using (command)
            {
                object r = command.ExecuteScalar();
                if (r != DBNull.Value && r != null)
                {
                    result = Convert.ToInt32(r);
                }
            }
        }
        return result;
    }
}