using Microsoft.Data.Sqlite;

public static class Database
{
	private const string USERS_TABLE = "Souls";

	public static async Task Run()
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		connection.Open();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $@"
			CREATE TABLE IF NOT EXISTS {USERS_TABLE} (
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
		command.CommandText = $"SELECT COUNT(*) FROM {USERS_TABLE} WHERE ChatId = $chatId LIMIT 1;";

		command.Parameters.AddWithValue("$chatId", chatId);

		long count = (long)(await command.ExecuteScalarAsync())!;

		if (count > 0) return true;
		else return false;
	}

	public static async Task AddOrUpdateUser((long chatId, string name, string age, string description, string photoId) user)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		connection.Open();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $@"INSERT INTO {USERS_TABLE} (ChatId, Name, Age, Description, PhotoId)
			VALUES($chatId, $name, $age, $description, $photoId)
			ON CONFLICT(ChatId) DO UPDATE SET
				Name = excluded.Name,
				Age = excluded.Age,
				Description = excluded.Description,
				PhotoId = excluded.PhotoId;
		;";

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
		command.CommandText = $"SELECT Name, Age, Description, PhotoId FROM {USERS_TABLE} WHERE ChatId = $chatId;";

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

	public static async Task<(string? id, string? name, string? age, string? description, string? photoId)> GetUserByOrderAsc(long id)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		connection.Open();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"SELECT Id, Name, Age, Description, PhotoId FROM {USERS_TABLE} WHERE Id > $id ORDER BY Id ASC LIMIT 1;";
		command.Parameters.AddWithValue("$id", id);

		using SqliteDataReader reader = await command.ExecuteReaderAsync();

		if (reader.Read())
		{
			(string id, string name, string age, string description, string photoId) user;
			user.id = reader["Id"].ToString()!;
			user.name = reader["Name"].ToString()!;
			user.age = reader["Age"].ToString()!;
			user.description = reader["Description"].ToString()!;
			user.photoId = reader["PhotoId"].ToString()!;

			return user;
		}

		return (null, null, null, null, null);
	}
}
