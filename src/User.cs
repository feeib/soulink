using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class UserState
{
	public long ChatId;

	public virtual async Task Create(Message msg)
	{
		Console.WriteLine($"Create: {GetType()}");
		ChatId = msg.Chat.Id;
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
}

public sealed class EditProfile : UserState
{
	private FormManager _formManager;

	public override async Task Create(Message msg)
	{
		await base.Create(msg);

		_formManager = new FormManager([new NameStep(), new AgeStep(), new DescriptionStep(), new PhotoStep()]);
	}

	public override async Task OnText(string text)
	{
		await base.OnText(text);

		if (await _formManager.ProcessInput(ChatId, text))
		{
			await Remove();
		}
	}

	public override async Task OnPhoto(PhotoSize[] photo)
	{
		await base.OnPhoto(photo);

		if (await _formManager.ProcessInput(ChatId, photo))
		{
			await Remove();
		}
	}

	public override async Task Remove()
	{
		await base.Remove();

		string name = _formManager.FormContext.Get("name").ToString();
		string age = _formManager.FormContext.Get("age").ToString();
		string description = _formManager.FormContext.Get("description").ToString();
		string photoId = _formManager.FormContext.Get("photoId").ToString();

		await Database.AddUserIfNotExists((ChatId, name, age, description, photoId));
	}
}

public sealed class ShowProfile : UserState
{
	public override async Task Create(Message msg)
	{
		await base.Create(msg);

		(string? name, string? age, string? description, string? photoId) user = await Database.GetUser(msg.Chat.Id);

		await Bot.TelegramBot!.SendPhoto(msg.Chat.Id, user.photoId!, $"{user.name}, {user.age}, {user.description}");
		await Remove();
	}

	public override async Task Remove()
	{
		await base.Remove();
	}
}
