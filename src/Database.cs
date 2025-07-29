using Microsoft.Data.Sqlite;

public static class Database
{
	private const string USERS_TABLE = "Souls";
	private const string USER_LIKES_TABLE = "Likes";
	private const string CATEGORIES_TABLE = "Categories";
	private const string USER_CATEGORIES_TABLE = "SoulCategories";

	public static async Task Run()
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $@"
			CREATE TABLE IF NOT EXISTS {USERS_TABLE} (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				ChatId INTEGER NOT NULL UNIQUE,
				Name VARCHAR(16),
				Age INTEGER,
				Description TEXT,
				PhotoId TEXT
			);
		";
		await command.ExecuteNonQueryAsync();

		command = connection.CreateCommand();
		command.CommandText = $@"
			CREATE TABLE IF NOT EXISTS {USER_LIKES_TABLE} (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				ChatId INTEGER NOT NULL,
				SoulChatId INTEGER NOT NULL,

				UNIQUE(ChatId, SoulChatId)
			);
		";

		await command.ExecuteNonQueryAsync();

		command = connection.CreateCommand();
		command.CommandText = $@"
			CREATE TABLE IF NOT EXISTS {CATEGORIES_TABLE} (
				Id INTEGER PRIMARY KEY,
				Title TEXT UNIQUE
			);
		";

		await command.ExecuteNonQueryAsync();

		command = connection.CreateCommand();
		command.CommandText = $@"
			CREATE TABLE IF NOT EXISTS {USER_CATEGORIES_TABLE} (
				UserId INTEGER,
				CategoryId INTEGER,
				FOREIGN KEY (UserId) REFERENCES Souls(Id),
				FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
				UNIQUE (UserId, CategoryId)
			);
		";

		await command.ExecuteNonQueryAsync();

		await AddCategory(1, "it");
		await AddCategory(2, "art");
		await AddCategory(3, "music");
	}

	public static async Task<bool> IsUserExist(long chatId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

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
		await connection.OpenAsync();

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

	public static async Task<(string name, string age, string description, string photoId)?> GetUserByChatId(long chatId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

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

		return null;
	}

	public static async Task<(string id, string chatId, string name, string age, string description, string photoId)?> GetUserByOrderAsc(long chatId, long id)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $@"SELECT u.Id, u.ChatId, u.Name, u.Age, u.Description, u.PhotoId
			FROM {USERS_TABLE} u
			JOIN {USER_CATEGORIES_TABLE} uc ON u.Id = uc.UserId
			WHERE u.Id > $id
			  	AND uc.CategoryId IN (
					SELECT CategoryId FROM {USER_CATEGORIES_TABLE}
					WHERE UserId = (SELECT Id FROM {USERS_TABLE} WHERE ChatId = $chatId)
			  	)
			ORDER BY u.Id ASC
			LIMIT 1;
		";
		command.Parameters.AddWithValue("$chatId", chatId);
		command.Parameters.AddWithValue("$id", id);

		using SqliteDataReader reader = await command.ExecuteReaderAsync();

		if (reader.Read())
		{
			(string id, string chatId, string name, string age, string description, string photoId) user;
			user.id = reader["Id"].ToString()!;
			user.chatId = reader["ChatId"].ToString()!;
			user.name = reader["Name"].ToString()!;
			user.age = reader["Age"].ToString()!;
			user.description = reader["Description"].ToString()!;
			user.photoId = reader["PhotoId"].ToString()!;

			return user;
		}

		return null;
	}

	public static async Task<(string id, string chatId)?> GetLike(long targetChatId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"SELECT Id, ChatId FROM {USER_LIKES_TABLE} WHERE SoulChatId = $chatId LIMIT 1;";
		command.Parameters.AddWithValue("$chatId", targetChatId);

		using SqliteDataReader reader = await command.ExecuteReaderAsync();

		if (reader.Read())
		{
			(string id, string chatId) like;
			like.id = reader["Id"].ToString()!;
			like.chatId = reader["ChatId"].ToString()!;

			return like;
		}
		return null;
	}

	public static async Task AddLike(long chatId, long targetChatId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"INSERT OR IGNORE INTO {USER_LIKES_TABLE} (ChatId, SoulChatId) VALUES ($chatId, $soulChatId);";
		command.Parameters.AddWithValue("$chatId", chatId);
		command.Parameters.AddWithValue("$soulChatId", targetChatId);

		await command.ExecuteNonQueryAsync();
	}

	public static async Task RemoveLike(long id)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"DELETE FROM {USER_LIKES_TABLE} WHERE Id = $id;";
		command.Parameters.AddWithValue("$id", id);

		await command.ExecuteNonQueryAsync();
	}

	public static async Task AddCategory(int categoryId, string title)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"INSERT OR IGNORE INTO {CATEGORIES_TABLE} (Id, Title) VALUES ($id, $title);";
		command.Parameters.AddWithValue("$id", categoryId);
		command.Parameters.AddWithValue("$title", title);

		await command.ExecuteNonQueryAsync();
	}

	public static async Task<List<(long, string)>> GetCategories()
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"SELECT * FROM {CATEGORIES_TABLE};";

		using SqliteDataReader reader = await command.ExecuteReaderAsync();
		List<(long, string)> res = new();

		while (await reader.ReadAsync())
		{
			res.Add(((long)reader["Id"], (string)reader["Title"]));
		}

		return res;
	}

	public static async Task AddUserCategoryByChatId(long chatId, int categoryId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();

		command.CommandText = $@"INSERT OR IGNORE INTO {USER_CATEGORIES_TABLE} (UserId, CategoryId)
			VALUES ((SELECT Id FROM {USERS_TABLE} WHERE ChatId = $chatId), $categoryId);
		";
		command.Parameters.AddWithValue("$chatId", chatId);
		command.Parameters.AddWithValue("$categoryId", categoryId);

		await command.ExecuteNonQueryAsync();
	}

	public static async Task<List<string>> GetUserCategoryByChatId(long chatId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();

		command.CommandText = $@"SELECT c.Title
			FROM {USER_CATEGORIES_TABLE} uc
			JOIN {CATEGORIES_TABLE} c
			ON uc.CategoryId = c.Id
			WHERE uc.UserId = (SELECT Id FROM {USERS_TABLE} WHERE ChatId = $chatId);
		";
		command.Parameters.AddWithValue("$chatId", chatId);

		using SqliteDataReader reader = await command.ExecuteReaderAsync();
		List<string> res = new();

		while (await reader.ReadAsync())
		{
			res.Add((string)reader["Title"]);
		}

		return res;
	}

	public static async Task RemoveUserCategoryByChatId(long chatId)
	{
		using SqliteConnection connection = new SqliteConnection(Environment.GetEnvironmentVariable("PATH_DATABASE"));
		await connection.OpenAsync();

		SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"DELETE FROM {USER_CATEGORIES_TABLE} WHERE UserId = (SELECT Id FROM {USERS_TABLE} WHERE ChatId = $chatId);";
		command.Parameters.AddWithValue("$chatId", chatId);

		await command.ExecuteNonQueryAsync();
	}
}
