using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public static class Bot
{
	public static TelegramBotClient? TelegramBot { get; private set; } = null!;
	private static User _user = null!;

	public static Dictionary<long, UserState>? Users { get; private set; } = null!;

	public static async Task Run()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();

		TelegramBot = new TelegramBotClient(Environment.GetEnvironmentVariable("BOT_TOKEN")!, cancellationToken: cts.Token);
		TelegramBot.OnUpdate += OnUpdate;
		TelegramBot.OnMessage += OnMessage;
		TelegramBot.OnError += OnError;

		Users = new Dictionary<long, UserState>();

		_user = await TelegramBot.GetMe();

		Console.WriteLine($"[id: {_user.Id}, name: {_user.FirstName}] bot is running... ");

		Console.ReadLine();
		cts.Cancel();
	}

	private static async Task OnUpdate(Update update)
	{
		if (update is { CallbackQuery: { } query })
		{
			if (Users!.ContainsKey(query.Message!.Chat.Id))
			{
				await Users[query.Message!.Chat.Id].OnUpdate(query);
			}
		}
	}

	private static async Task OnMessage(Message msg, UpdateType updateType)
	{
		if (msg.Text is not null)
		{
			if (msg.Text.Equals("/start"))
			{
				if (!await Database.IsUserExist(msg.Chat.Id))
				{
					await CreateSoul(msg, new EditProfile());
				}
			}
			else if (msg.Text.Equals("/profile"))
			{
				if (await Database.IsUserExist(msg.Chat.Id))
				{
					await CreateSoul(msg, new ShowProfile());
				}
			}
			else if (msg.Text.Equals("/find"))
			{
				if (await Database.IsUserExist(msg.Chat.Id))
				{
					await CreateSoul(msg, new WatchProfile());
				}
			}
			if (Users!.ContainsKey(msg.Chat.Id))
			{
				Users![msg.Chat.Id]?.OnText(msg.Text);
			}
		}
		else if (msg.Photo is not null)
		{
			if (Users!.ContainsKey(msg.Chat.Id))
			{
				Users![msg.Chat.Id]?.OnPhoto(msg.Photo);
			}
		}
	}

#pragma warning disable 1998
	private static async Task OnError(Exception exception, HandleErrorSource source)
	{
		Console.WriteLine($"OnError: {exception}");
	}
#pragma warning restore 1998

	private static async Task CreateSoul(Message msg, UserState state)
	{
		Users!.Add(msg.Chat.Id, state);
		await state.Create(msg);
	}
}
