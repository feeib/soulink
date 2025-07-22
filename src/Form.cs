using Telegram.Bot;
using Telegram.Bot.Types;

public abstract class FormStep
{
	public abstract string Question { get; }

	public abstract bool Validate(object obj, out string? error);
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

		await Bot.TelegramBot!.SendMessage(chatId, CurrentStep!.Question);
	}

	public async Task<bool> ProcessInput(long chatId, object input)
	{
		if (!_start) return false;

		if (CurrentStep!.Validate(input, out string? error))
		{
			CurrentStep.SaveAnswer(FormContext, input);

			_currentIndex++;

			if (CurrentStep is null) return true;
			await Bot.TelegramBot!.SendMessage(chatId, CurrentStep.Question);
		}
		else
		{
			await Bot.TelegramBot!.SendMessage(chatId, error!);
		}

		return false;
	}
}

public class NameStep : FormStep
{
	public override string Question => "Як тебе звати?";

	public override bool Validate(object obj, out string? error)
	{
		if (obj is string text)
		{
			error = string.IsNullOrWhiteSpace(text) ? "Ім'я не може бути порожнім." : null;
		}
		else
		{
			error = "Ім'я має бути текстом.";
		}
		return error is null;
	}

	public override void SaveAnswer(FormContext context, object obj)
	{
		context.Set("name", obj);
	}
}

public class AgeStep : FormStep
{
	public override string Question => "Скільки тобі років?";

	public override bool Validate(object obj, out string? error)
	{
		if (obj is string text && ushort.TryParse(text, out ushort age) && (age < 80 && age > 6))
		{
			error = null;
			return true;
		}
		else
		{
			error = "Вік має бути числом, 6 - 80.";
			return false;
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

	public override bool Validate(object obj, out string? error)
	{
		if (obj is string text)
		{
			error = string.IsNullOrWhiteSpace(text) || text.Length < 100 ? "Опис не може бути пустим, мінімум 100 символів!" : null;
		}
		else
		{
			error = "Має бути текст.";
		}
		return error is null;
	}

	public override void SaveAnswer(FormContext context, object obj)
	{
		context.Set("description", ((string)obj).Trim());
	}
}

public class PhotoStep : FormStep
{
	public override string Question => "Додай фото.";

	public override bool Validate(object obj, out string? error)
	{
		if (obj is PhotoSize[] photo)
		{
			error = null;
			return true;
		}
		else
		{
			error = "Має бути фото.";
			return false;
		}
	}

	public override void SaveAnswer(FormContext context, object obj)
	{
		context.Set("photoId", ((PhotoSize[])obj)[^1].FileId);
	}
}
