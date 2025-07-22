using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class UserState
{
	public long ChatId;

#pragma warning disable CS1998
	public virtual async Task Create(Message msg)
	{
		Console.WriteLine($"Create: {GetType()}");
		ChatId = msg.Chat.Id;
	}

	public virtual async Task OnUpdate(CallbackQuery query)
	{
		Console.WriteLine($"OnUpdate: {GetType()}");
	}

	public virtual async Task OnText(string text)
	{
		Console.WriteLine($"OnText: {GetType()}");
	}

	public virtual async Task OnPhoto(PhotoSize[] photo)
	{
		Console.WriteLine($"OnPhoto: {GetType()}");
	}

	public virtual async Task Remove()
	{
		Console.WriteLine($"Remove: {GetType()}");

		if (Bot.Users!.ContainsKey(ChatId))
		{
			Bot.Users!.Remove(ChatId);
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

		FormManager = new FormManager([new NameStep(), new AgeStep(), new DescriptionStep(), new PhotoStep()]);
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

		(string? name, string? age, string? description, string? photoId) user = await Database.GetUser(msg.Chat.Id);

		await Bot.TelegramBot!.SendPhoto(msg.Chat.Id, user.photoId!, $"{user.name}, {user.age}, {user.description}", replyMarkup: new InlineKeyboardButton[] { "Edit", "Back" });
	}

	public override async Task OnUpdate(CallbackQuery query)
	{
		if (query.Data is null || query.Message is null) return;

		if (query.Data.Equals("Edit"))
		{
			await base.Remove();
			await Bot.CreateSoul(query.Message, new EditProfile(), async (state) =>
			{
				await state.Create(query.Message);
				await ((EditProfile)state).FormManager.Start(query.Message.Chat.Id);
			});
		}
		else if (query.Data.Equals("Back"))
		{
			await base.Remove();
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

		if (query.Data.Equals("Next"))
		{
			Show();
		}
		else if (query.Data.Equals("Like"))
		{
			await Bot.TelegramBot!.AnswerCallbackQuery(query.Id, $"Ти тицьнув вподобайку {_savedSouls[query.Message.Id]}.");
		}
		else if (query.Data.Equals("Stop"))
		{
			await base.Remove();
		}
	}

	private async void Show()
	{
		(string? id, string? name, string? age, string? description, string? photoId) user = await Database.GetUserByOrderAsc(_lastUserId);

		if (user.id is not null)
		{
			_lastUserId = long.Parse(user.id);

			Message msg = await Bot.TelegramBot!.SendPhoto(ChatId, user.photoId!, $"id:{user.id}, {user.name}, {user.age}, {user.description}", replyMarkup: new InlineKeyboardButton[] { "Next", "Like", "Stop" });

			_savedSouls.Add(msg.Id, _lastUserId);
		}
	}
}
