using System;

namespace ListCreateBot {
    class Program {
        static void Main(string[] args) {
            var botToken = GetBotToken();

            Console.WriteLine(botToken);

            Console.ReadLine();
        }

        private static string GetBotToken() {
            DotNetEnv.Env.TraversePath().Load();
            return Environment.GetEnvironmentVariable("BOT_TOKEN");
        }
    }
}
