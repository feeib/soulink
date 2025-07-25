using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public class UserState
{
	public long ChatId { get; private set; }
	public DateTime LastActive { get; private set; }

#pragma warning disable CS1998
	public virtual async Task Create(Message msg)
	{
		Console.WriteLine($"Create: {GetType()}");
		ChatId = msg.Chat.Id;
		LastActive = DateTime.UtcNow;
	}

	public virtual async Task OnUpdate(CallbackQuery query)
	{
		Console.WriteLine($"OnUpdate: {GetType()}");
		LastActive = DateTime.UtcNow;
	}

	public virtual async Task OnText(string text)
	{
		Console.WriteLine($"OnText: {GetType()}");
		LastActive = DateTime.UtcNow;
	}

	public virtual async Task OnPhoto(PhotoSize[] photo)
	{
		Console.WriteLine($"OnPhoto: {GetType()}");
		LastActive = DateTime.UtcNow;
	}

	public virtual async Task Remove()
	{
		if (Bot.Users!.TryRemove(ChatId, out _))
		{
			Console.WriteLine($"Remove: {GetType()}");
		}
		else
		{
			Console.WriteLine($"No remove: {GetType()}");
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
			await Bot.TelegramBot!.SendMessage(msg.Chat.Id, $"Встанови username на початок...");
			await Remove();
			return;
		}

		FormManager = new FormManager([new NameStep(), new AgeStep(), new DescriptionStep(), new PhotoStep()]);
		await FormManager.Start(msg.Chat.Id);
	}

	public override async Task OnText(string text)
	{
		await base.OnText(text);

		if (await FormManager.ProcessInput(ChatId, text))
		{
			await Remove();
		}
	}

	public override async Task OnPhoto(PhotoSize[] photo)
	{
		await base.OnPhoto(photo);

		if (await FormManager.ProcessInput(ChatId, photo))
		{
			await Remove();
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

		await Database.AddOrUpdateUser((ChatId, name, age, description, photoId));
	}
}

public sealed class ShowProfile : UserState
{
	public override async Task Create(Message msg)
	{
		await base.Create(msg);

		(string name, string age, string description, string photoId)? user = await Database.GetUserByChatId(msg.Chat.Id);

		if (user.HasValue)
		{
			await Bot.TelegramBot!.SendPhoto(msg.Chat.Id, user.Value.photoId, $"{user.Value.name}, {user.Value.age}, {user.Value.description}", replyMarkup: new InlineKeyboardButton[] { "📝" });
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
	private Dictionary<int, long> _savedSouls = new Dictionary<int, long>(); //message id, user db id

	private long _lastUserId = -1;

	public override async Task Create(Message msg)
	{
		await base.Create(msg);

		Show();
	}

	public override async Task OnUpdate(CallbackQuery query)
	{
		await base.OnUpdate(query);

		if (query.Message is null || query.Data is null) return;

		if (!_savedSouls.ContainsKey(query.Message.Id))
		{
			await Bot.TelegramBot!.AnswerCallbackQuery(query.Id, "Та анкета вигасла.");
			return;
		}

		if (query.Data.Equals("➡️"))
		{
			Show();
		}
		else if (query.Data.Equals("👍"))
		{
			await Database.AddLike(query.Message.Chat.Id, _savedSouls[query.Message.Id]);
			await Bot.TelegramBot!.SendMessage(_savedSouls[query.Message.Id], "Ти дістав вподобайку!", replyMarkup: new InlineKeyboardButton[] { "📤" });
		}
	}

	private async void Show()
	{
		(string id, string chatId, string name, string age, string description, string photoId)? user = await Database.GetUserByOrderAsc(_lastUserId);

		if (user.HasValue)
		{
			_lastUserId = long.Parse(user.Value.id);

			Message msg = await Bot.TelegramBot!.SendPhoto(ChatId, user.Value.photoId, $"{user.Value.name}, {user.Value.age}, {user.Value.description}", replyMarkup: new InlineKeyboardButton[] { "➡️", "👍" });

			_savedSouls.Add(msg.Id, long.Parse(user.Value.chatId));
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

			await Bot.TelegramBot!.SendMessage(ChatId, $"Пиши: @{chat.Username} Удачі!");
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
				await Bot.TelegramBot!.SendPhoto(ChatId, user.Value.photoId, $"{user.Value.name}, {user.Value.age}, {user.Value.description}", replyMarkup: new InlineKeyboardButton[] { "\U0001F44E", "\U0001F44D" });
			}
		}
		else
		{
			await Bot.TelegramBot!.SendMessage(ChatId, "Пусто тут, більше нічого немає");
			await Remove();
		}
	}
}
