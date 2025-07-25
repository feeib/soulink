using System;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public static class Bot
{
	public static TelegramBotClient? TelegramBot { get; private set; } = null!;
	private static User _user = null!;

	public static ConcurrentDictionary<long, UserState>? Users { get; private set; } = null!;

	private const int CLEANUP_USERS_TIME = 1;

	public static async Task Run()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();

		TelegramBot = new TelegramBotClient(Environment.GetEnvironmentVariable("BOT_TOKEN")!, cancellationToken: cts.Token);
		TelegramBot.OnUpdate += OnUpdate;
		TelegramBot.OnMessage += OnMessage;
		TelegramBot.OnError += OnError;

		await TelegramBot.SetMyCommands(new[]{
			new BotCommand { Command = "find", Description = "🔍шукай свого" },
			new BotCommand { Command = "profile", Description = "👤твоя \"крінжова\" анкета" },
			new BotCommand { Command = "check", Description = "👀глянути вподобайки" },
		});

		Users = new ConcurrentDictionary<long, UserState>();

		_user = await TelegramBot.GetMe();

		Console.WriteLine($"[id: {_user.Id}, name: {_user.FirstName}] bot is running... ");

		_ = Task.Run(async () =>
		{
			while (true)
			{
				await Task.Delay(TimeSpan.FromMinutes(CLEANUP_USERS_TIME));

				DateTime now = DateTime.UtcNow;
				int removedCount = 0;
				int usersCount = Users.Count;

				foreach (KeyValuePair<long, UserState> pair in Users)
				{
					if ((now - pair.Value.LastActive).TotalMinutes > CLEANUP_USERS_TIME)
					{
						await pair.Value.Remove();
						removedCount++;
					}
				}

				Console.WriteLine($"Removed {removedCount} users of {usersCount}");
			}
		});

		Console.ReadLine();
		cts.Cancel();
	}

	private static async Task OnUpdate(Update update)
	{
		if (update is { CallbackQuery: { } query })
		{
			if (query.Data is not null && query.Message is not null && query.Data.Equals("📤"))
			{
				await ChangeState(query.Message, new ViewLikedProfile());
			}
			else if (Users!.ContainsKey(query.Message!.Chat.Id))
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
					await ChangeState(msg, new EditProfile());
				}
			}
			else if (msg.Text.Equals("/profile"))
			{
				if (await Database.IsUserExist(msg.Chat.Id))
				{
					await ChangeState(msg, new ShowProfile());
				}
			}
			else if (msg.Text.Equals("/find"))
			{
				if (await Database.IsUserExist(msg.Chat.Id))
				{
					await ChangeState(msg, new ViewProfile());
				}
			}
			else if (msg.Text.Equals("/check"))
			{
				if (await Database.IsUserExist(msg.Chat.Id))
				{
					await ChangeState(msg, new ViewLikedProfile());
				}
			}
			else if (Users!.ContainsKey(msg.Chat.Id))
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

	private static async Task CreateState(Message msg, UserState state)
	{
		if (Users!.TryAdd(msg.Chat.Id, state))
		{
			await state.Create(msg);
		}
	}

	public static async Task ChangeState(Message msg, UserState newState)
	{
		if (Users!.ContainsKey(msg.Chat.Id))
		{
			await Users![msg.Chat.Id].Remove();
		}
		await CreateState(msg, newState);
	}
}
