using DotNetEnv;

public sealed class Program
{
	private static async Task Main()
	{
		Env.Load();

		await Database.Run();
		await Bot.Run();
	}
}
