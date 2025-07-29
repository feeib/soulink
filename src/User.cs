using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public class Soul
{
	public UserState State { get; set; } = null!;
	public Message LastMessage { get; set; } = null!;
}

public class UserState
{
	public long ChatId { get; private set; }
	public DateTime LastActive { get; private set; }

#pragma warning disable CS1998
	public virtual async Task Create(Message msg)
	{
		ChatId = msg.Chat.Id;
		LastActive = DateTime.UtcNow;
		Console.WriteLine($"[{LastActive}] {ChatId} Create: {GetType()}");
	}

	public virtual async Task OnUpdate(CallbackQuery query)
	{
		LastActive = DateTime.UtcNow;
		Console.WriteLine($"[{LastActive}] {ChatId} OnUpdate: {GetType()}");
	}

	public virtual async Task OnMessage(Message message)
	{
		LastActive = DateTime.UtcNow;
		Console.WriteLine($"[{LastActive}] {ChatId} OnMessage: {GetType()}");
	}

	public virtual async Task Remove()
	{
		if (Bot.Users!.TryGetValue(ChatId, out Soul? soul))
		{
			soul.State = null!;
			Console.WriteLine($"[{LastActive}] {ChatId} Remove: {GetType()}");
		}
		else
		{
			Console.WriteLine($"[{LastActive}] {ChatId} No remove: {GetType()}");
		}
	}
#pragma warning restore CS1998
}

public sealed class EditProfile : UserState
{
	public FormManager FormManager { get; private set; } = null!;

	public override async Task Create(Message msg)
	{
		await base.Create(msg);

		if (string.IsNullOrWhiteSpace(msg.Chat.Username))
		{
			await Bot.SendMessage(msg.Chat.Id, $"Встанови username в налаштуваннях на початок...");
			await Remove();
			return;
		}

		FormManager = new FormManager([
			new CategoryStep() { Categories = await Database.GetCategories()},
			new NameStep(),
			new AgeStep(),
			new DescriptionStep(),
			new PhotoStep()
			]);

		await FormManager.Start(msg.Chat.Id);
	}

	public override async Task OnUpdate(CallbackQuery query)
	{
		await base.OnUpdate(query);

		if (await FormManager.ProcessInput(ChatId, query))
		{
			await Remove();
		}
	}

	public override async Task OnMessage(Message message)
	{
		await base.OnMessage(message);

		if (message.Text is string text)
		{
			if (await FormManager.ProcessInput(ChatId, text))
			{
				await Remove();
			}
		}
		else if (message.Photo is PhotoSize[] photo)
		{
			if (await FormManager.ProcessInput(ChatId, photo))
			{
				await Remove();
			}
		}
	}

	public override async Task Remove()
	{
		await base.Remove();

		if (FormManager is null) return;

		string name = FormManager.FormContext.Get<string>("name");
		string age = FormManager.FormContext.Get<string>("age");
		string description = FormManager.FormContext.Get<string>("description");
		string photoId = FormManager.FormContext.Get<string>("photoId");
		List<long> categories = FormManager.FormContext.Get<List<long>>("categories");

		await Database.AddOrUpdateUser((ChatId, name, age, description, photoId));
		await Database.RemoveUserCategoryByChatId(ChatId);

		foreach (int category in categories)
		{
			await Database.AddUserCategoryByChatId(ChatId, category);
		}
	}
}

public sealed class ShowProfile : UserState
{
	public override async Task Create(Message msg)
	{
		await base.Create(msg);

		(string name, string age, string description, string photoId)? user = await Database.GetUserByChatId(ChatId);
		string categories = string.Join(", ", await Database.GetUserCategoryByChatId(ChatId));

		if (user.HasValue)
		{
			await Bot.SendPhoto(ChatId, user.Value.photoId, $"{user.Value.name}, {user.Value.age}\n{user.Value.description}\n🏷️: {categories}", replyMarkup: new InlineKeyboardButton[] { "📝" });
		}
	}

	public override async Task OnUpdate(CallbackQuery query)
	{
		if (query.Data is null || query.Message is null) return;

		if (query.Data.Equals("📝"))
		{
			await Bot.ChangeState(query.Message, new EditProfile());
		}
	}

	public override async Task Remove()
	{
		await base.Remove();
	}
}

public sealed class ViewProfile : UserState
{
	private long _lastUserId = -1;
	private long _lastUserChatId = -1;

	public override async Task Create(Message msg)
	{
		await base.Create(msg);

		await ShowNext();
	}

	public override async Task OnUpdate(CallbackQuery query)
	{
		await base.OnUpdate(query);

		if (query.Message is null || query.Data is null) return;

		if (query.Data.Equals("➡️"))
		{
			await ShowNext();
		}
		else if (query.Data.Equals("👍"))
		{
			await Database.AddLike(ChatId, _lastUserChatId);
			await Bot.SendMessage(_lastUserChatId, "Ти дістав вподобайку!", replyMarkup: new InlineKeyboardButton[] { "📤" });
			await ShowNext();
		}
	}

	private async Task ShowNext()
	{
		(string id, string chatId, string name, string age, string description, string photoId)? user = await Database.GetUserByOrderAsc(ChatId, _lastUserId);

		if (user.HasValue)
		{
			_lastUserId = long.Parse(user.Value.id);
			_lastUserChatId = long.Parse(user.Value.chatId);
			string categories = string.Join(", ", await Database.GetUserCategoryByChatId(_lastUserChatId));

			await Bot.SendPhoto(ChatId, user.Value.photoId, $"{user.Value.name}, {user.Value.age}\n{user.Value.description}\n🏷️: {categories}", replyMarkup: new InlineKeyboardButton[] { "➡️", "👍" });
		}
		else
		{
			await Bot.SendMessage(ChatId, "Пусто.");
		}
	}
}

public sealed class ViewLikedProfile : UserState
{
	private long _lastLikeId;
	private long _lastChatId;

	public override async Task Create(Message msg)
	{
		await base.Create(msg);

		await ShowNext();
	}

	public override async Task OnUpdate(CallbackQuery query)
	{
		await base.OnUpdate(query);

		if (query.Message is null || query.Data is null) return;

		if (query.Data.Equals("👎"))
		{
			await Database.RemoveLike(_lastLikeId);
			await ShowNext();
		}
		else if (query.Data.Equals("👍"))
		{
			ChatFullInfo chat = await Bot.TelegramBot!.GetChat(_lastChatId);

			await Bot.SendMessage(ChatId, $"Пиши: @{chat.Username} Удачі!");
			await Database.RemoveLike(_lastLikeId);
			await Remove();
		}
	}

	private async Task ShowNext()
	{
		(string id, string chatId)? like = await Database.GetLike(ChatId);

		if (like.HasValue)
		{
			_lastLikeId = long.Parse(like.Value.id);
			_lastChatId = long.Parse(like.Value.chatId);

			(string name, string age, string description, string photoId)? user = await Database.GetUserByChatId(long.Parse(like.Value.chatId));

			if (user.HasValue)
			{
				await Bot.SendPhoto(ChatId, user.Value.photoId, $"{user.Value.name}, {user.Value.age}, {user.Value.description}", replyMarkup: new InlineKeyboardButton[] { "\U0001F44E", "\U0001F44D" });
			}
		}
		else
		{
			await Bot.SendMessage(ChatId, "Пусто тут, більше нічого немає");
			await Remove();
		}
	}
}
