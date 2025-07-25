using DotNetEnv;

public sealed class Program
{
	private static async Task Main()
	{
		Env.Load();

		await Database.Run();
		await Bot.Run();

		// Random rand = new Random();
		// for (int i = 0; i < 10; i++)
		// {
		// 	Database.AddOrUpdateUser((rand.NextInt64(0, 100000), rand.NextInt64(0, 9999999).ToString(), rand.NextInt64(7, 80).ToString(), "ashdjkflhasdjkfhlakjsdhfjlkasdhfkjlasdhfkjlahsdkljfhaskljdhfkljasdhflkjashdfkjlahsdlkjfhasdlkjfhaskljdfhjkalsdhflkajsdhfljkahsdf", "AgACAgIAAxkBAAIC-Gh9Y3isd0MWVm5su4kyaX3B0_3LAAJV_zEbtnbpS90p6zEUKEPjAQADAgADbQADNgQ"));
		// }
	}
}
