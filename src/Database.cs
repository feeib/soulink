using Microsoft.Data.Sqlite;

public static class Database
{
	public static async Task Run()
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		connection.Open();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = @"
			CREATE TABLE IF NOT EXISTS Souls (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				ChatId INTEGER NOT NULL UNIQUE,
				Name VARCHAR(16),
				Age INT,
				Description TEXT,
				PhotoId TEXT
			);
		";

		await command.ExecuteNonQueryAsync();
	}

	public static async Task<bool> IsUserExist(long chatId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		connection.Open();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM Souls WHERE ChatId = $chatId LIMIT 1;";

		command.Parameters.AddWithValue("$chatId", chatId);

		long count = (long)(await command.ExecuteScalarAsync())!;

		if (count > 0) return true;
		else return false;
	}

	public static async Task AddUserIfNotExists((long chatId, string name, string age, string description, string photoId) user)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		connection.Open();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = "INSERT OR IGNORE INTO Souls (ChatId, Name, Age, Description, PhotoId) VALUES($chatId, $name, $age, $description, $photoId);";

		command.Parameters.AddWithValue("$chatId", user.chatId);
		command.Parameters.AddWithValue("$name", user.name);
		command.Parameters.AddWithValue("$age", user.age);
		command.Parameters.AddWithValue("$description", user.description);
		command.Parameters.AddWithValue("$photoId", user.photoId);

		await command.ExecuteNonQueryAsync();
	}

	public static async Task<(string? name, string? age, string? description, string? photoId)> GetUser(long chatId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		connection.Open();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = "SELECT Name, Age, Description, PhotoId FROM Souls WHERE ChatId = $chatId;";

		command.Parameters.AddWithValue("$chatId", chatId);

		using SqliteDataReader reader = await command.ExecuteReaderAsync();

		if (reader.Read())
		{
			(string name, string age, string description, string photoId) user;
			user.name = reader["Name"].ToString()!;
			user.age = reader["Age"].ToString()!;
			user.description = reader["Description"].ToString()!;
			user.photoId = reader["PhotoId"].ToString()!;

			return user;
		}

		return (null, null, null, null);
	}
}
