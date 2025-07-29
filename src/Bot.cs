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

	public static ConcurrentDictionary<long, Soul>? Users { get; private set; } = null!;

	private const int CLEANUP_USERS_TIME = 100;

	public static async Task Run()
	{

		using CancellationTokenSource cts = new CancellationTokenSource();

		Users = new ConcurrentDictionary<long, Soul>();

		TelegramBot = new TelegramBotClient(Environment.GetEnvironmentVariable("BOT_TOKEN")!, cancellationToken: cts.Token);
		TelegramBot.OnUpdate += OnUpdate;
		TelegramBot.OnMessage += OnMessage;
		TelegramBot.OnError += OnError;

		await TelegramBot.SetMyCommands(new[]{
			new BotCommand { Command = "find", Description = "🔍шукай свого" },
			new BotCommand { Command = "profile", Description = "👤твоя \"крінжова\" анкета" },
			new BotCommand { Command = "check", Description = "👀глянути вподобайки" },
		});

		_user = await TelegramBot.GetMe();

		Console.WriteLine($"[id: {_user.Id}, name: {_user.FirstName}] bot is running... ");

		_ = Task.Run(async () =>
		{
			while (true)
			{
				await Task.Delay(TimeSpan.FromSeconds(CLEANUP_USERS_TIME));

				DateTime now = DateTime.UtcNow;
				int removedCount = 0;
				int usersCount = Users.Count;

				foreach (KeyValuePair<long, Soul> pair in Users)
				{
					if ((now - pair.Value.State.LastActive).TotalSeconds > CLEANUP_USERS_TIME)
					{
						await SendMessage(pair.Key, $"Не активний. Почнеш спочатку");
						await Task.Delay(100);
						if (Users.TryRemove(pair.Key, out _))
						{
							removedCount++;
						}
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
				await Users[query.Message!.Chat.Id].State.OnUpdate(query);
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
					return;
				}
			}
			else if (msg.Text.Equals("/profile"))
			{
				if (await Database.IsUserExist(msg.Chat.Id))
				{
					await ChangeState(msg, new ShowProfile());
					return;
				}
			}
			else if (msg.Text.Equals("/find"))
			{
				if (await Database.IsUserExist(msg.Chat.Id))
				{
					await ChangeState(msg, new ViewProfile());
					return;
				}
			}
			else if (msg.Text.Equals("/check"))
			{
				if (await Database.IsUserExist(msg.Chat.Id))
				{
					await ChangeState(msg, new ViewLikedProfile());
					return;
				}
			}
		}
		if (Users!.ContainsKey(msg.Chat.Id))
		{
			Users![msg.Chat.Id]?.State?.OnMessage(msg);
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
		if (Users!.TryAdd(msg.Chat.Id, new Soul() { State = state }))
		{
			await state.Create(msg);
		}
		else if (Users!.TryGetValue(msg.Chat.Id, out Soul? soul))
		{
			soul.State = state;
			await state.Create(msg);
		}
	}

	public static async Task ChangeState(Message msg, UserState newState)
	{
		if (Users!.TryGetValue(msg.Chat.Id, out Soul? soul) && soul.State is not null)
		{
			await soul.State.Remove();
		}
		await CreateSoul(msg, newState);
	}

	public static async Task RemoveReplyKeyboard(Message message)
	{
		await TelegramBot!.EditMessageReplyMarkup(
			chatId: message.Chat.Id,
			messageId: message.MessageId,
			replyMarkup: null
		);
	}

	public static async Task SendMessage(long chatId, string text, ReplyMarkup? replyMarkup = null)
	{
		if (Users!.TryGetValue(chatId, out Soul? soul))
		{
			Message msg = soul.LastMessage;
			if (msg is { ReplyMarkup: { } rm })
			{
				await TelegramBot!.EditMessageReplyMarkup(
					chatId: msg.Chat.Id,
					messageId: msg.MessageId,
					replyMarkup: null
				);
			}

			soul.LastMessage = await TelegramBot!.SendMessage(chatId, text, replyMarkup: replyMarkup);
		}
	}

	public static async Task SendPhoto(long chatId, InputFile photo, string text, ReplyMarkup? replyMarkup = null)
	{
		if (Users!.TryGetValue(chatId, out Soul? soul))
		{
			Message msg = soul.LastMessage;
			if (msg is { ReplyMarkup: { } rm })
			{
				await TelegramBot!.EditMessageReplyMarkup(
					chatId: msg.Chat.Id,
					messageId: msg.MessageId,
					replyMarkup: null
				);
			}

			soul.LastMessage = await TelegramBot!.SendPhoto(chatId, photo, text, replyMarkup: replyMarkup);
		}
	}
}
