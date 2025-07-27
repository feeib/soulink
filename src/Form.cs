using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public abstract class FormStep
{
	public abstract string Question { get; }

	public virtual async Task ShowQuestion(long chatId)
	{
		await Bot.SendMessage(chatId, Question);
	}
	public abstract Task<(bool, string?)> Validate(object obj);
	public abstract void SaveAnswer(FormContext formContext, object obj);
}

public class FormContext
{
	public Dictionary<string, object> Answers { get; } = new();

	public void Set(string key, object obj) => Answers[key] = obj;
	public T Get<T>(string key)
	{
		if (Answers.TryGetValue(key, out var value) && value is T typed)
			return typed;

		throw new InvalidOperationException($"No value assigned to key '{key}'");
	}
}

public class FormManager
{
	private readonly List<FormStep> _steps;
	public FormContext FormContext { get; init; }

	private ushort _currentIndex;
	private bool _start;

	public FormStep? CurrentStep => _currentIndex < _steps.Count ? _steps[_currentIndex] : null;

	public FormManager(IEnumerable<FormStep> steps)
	{
		_steps = steps.ToList();
		FormContext = new FormContext();
	}

	public async Task Start(long chatId)
	{
		_start = true;

		await CurrentStep!.ShowQuestion(chatId);
	}

	public async Task<bool> ProcessInput(long chatId, object input)
	{
		if (!_start) return false;

		(bool validate, string? error) result = await CurrentStep!.Validate(input);

		if (result.validate)
		{
			CurrentStep.SaveAnswer(FormContext, input);

			_currentIndex++;

			if (CurrentStep is null) return true;
			await CurrentStep!.ShowQuestion(chatId);
		}
		else
		{
			if (result.error is not null)
				await Bot.SendMessage(chatId, result.error);
		}
		return false;
	}
}

public class NameStep : FormStep
{
	public override string Question => "Як тебе звати?";

	public override Task<(bool, string?)> Validate(object obj)
	{
		if (obj is string text && !string.IsNullOrWhiteSpace(text))
		{
			return Task.FromResult<(bool, string?)>((true, null));
		}
		else
		{
			return Task.FromResult<(bool, string?)>((false, "Ім'я має бути текстом i не може бути порожнім."));
		}
	}

	public override void SaveAnswer(FormContext context, object obj)
	{
		context.Set("name", obj);
	}
}

public class AgeStep : FormStep
{
	public override string Question => "Скільки тобі років?";

	public override Task<(bool, string?)> Validate(object obj)
	{
		if (obj is string text && ushort.TryParse(text, out ushort age) && (age <= 80 && age >= 6))
		{
			return Task.FromResult<(bool, string?)>((true, null));
		}
		else
		{
			return Task.FromResult<(bool, string?)>((false, "Вік має бути числом, 6 - 80."));
		}
	}

	public override void SaveAnswer(FormContext context, object obj)
	{
		context.Set("age", obj);
	}
}

public class DescriptionStep : FormStep
{
	public override string Question => "Напиши щось про себе.";

	public override Task<(bool, string?)> Validate(object obj)
	{
		if (obj is string text && !string.IsNullOrWhiteSpace(text) && text.Length > 100)
		{
			return Task.FromResult<(bool, string?)>((true, null));
		}
		else
		{
			return Task.FromResult<(bool, string?)>((false, "Опис не може бути пустим, мінімум 100 символів!"));
		}
	}

	public override void SaveAnswer(FormContext context, object obj)
	{
		context.Set("description", ((string)obj).Trim());
	}
}

public class PhotoStep : FormStep
{
	public override string Question => "Додай фото.";

	public override Task<(bool, string?)> Validate(object obj)
	{
		if (obj is PhotoSize[] photo)
		{
			return Task.FromResult<(bool, string?)>((true, null));
		}
		else
		{
			return Task.FromResult<(bool, string?)>((false, "Має бути фото."));
		}
	}

	public override void SaveAnswer(FormContext context, object obj)
	{
		context.Set("photoId", ((PhotoSize[])obj)[^1].FileId);
	}
}

public class CategoryStep : FormStep
{
	public override string Question => "Вибери своє зацікавлення.\n1. Програмування\n2. Малювання\n3. Музика";

	private string[] _categories = new[] { "IT", "ART", "MUSIC" };
	private List<string> _selectedCategories = new List<string>();

	public InlineKeyboardMarkup BuildKeyboard()
	{
		List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>() { new(), new() };

		foreach (string category in _categories)
		{
			bool selected = _selectedCategories.Contains(category);
			string emoji = selected ? "🟢" : "🔴";

			var button = InlineKeyboardButton.WithCallbackData($"{emoji} {category}", $"toggle:{category}");
			buttons[0].Add(button);
		}

		var doneButton = InlineKeyboardButton.WithCallbackData("✅ Готово", "done");
		buttons[1].Add(doneButton);

		return new InlineKeyboardMarkup(buttons);
	}

	public override async Task ShowQuestion(long chatId)
	{
		await Bot.SendMessage(chatId, Question, replyMarkup: BuildKeyboard());
	}

	public override async Task<(bool, string?)> Validate(object obj)
	{
		if (obj is CallbackQuery query)
		{
			if (query is { Data: { } data, Message: { } msg })
			{
				if (data.StartsWith("toggle:"))
				{
					var tag = data.Split(':')[1];

					if (_selectedCategories.Contains(tag))
						_selectedCategories.Remove(tag);
					else
						_selectedCategories.Add(tag);

					await Bot.TelegramBot!.EditMessageReplyMarkup(
						chatId: msg.Chat.Id,
						messageId: msg.MessageId,
						replyMarkup: BuildKeyboard()
					);
					return (false, null);
				}
				else if (data.Equals("done"))
				{
					return (true, null);
				}
			}
			return (false, "Ну це буде пізда, якщо це з`явиться!!!");
		}
		else
		{
			return (false, "Має бути текст.");
		}
	}

	public override void SaveAnswer(FormContext context, object obj)
	{
		context.Set("category", _selectedCategories);
	}
}
