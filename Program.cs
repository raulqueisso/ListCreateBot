using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ListCreateBot {
    struct BotUpdate {
        public long chatId;
        public string waitedInput;
        public string savedList;
    }
    class Program {
        static string fileName = "updates.json";
        static List<BotUpdate> botUpdates = new List<BotUpdate>();

        static async Task Main(string[] args) {
            var botToken = GetBotToken();
            var botClient = new TelegramBotClient(botToken);

            // Read all saved updates
            try {
                var botUpdateString = System.IO.File.ReadAllText(fileName);

                botUpdates = JsonConvert.DeserializeObject<List<BotUpdate>>(botUpdateString) ?? botUpdates;
            } catch (Exception ex) {
                Console.WriteLine($"Error reading or deserializing {ex}");
            }

            using var cts = new CancellationTokenSource();

            StartBot(botClient, cts);

            var me = await botClient.GetMeAsync();
            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken) {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Type != UpdateType.Message) {
                return;
            }
            // Only process text messages
            if (update.Message.Type != MessageType.Text) {
                return;
            }

            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;

            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

            string text;

            if (messageText == "/add") {
                text = "OK! Send one or multiple items separated by a comma. Like this:\n\nItem 1, item 2, item 3";

                WriteUpdate(chatId, "/add", null);
            } else {
                var botUpdatesId = GetBotUpdatesId(chatId);

                if (botUpdatesId != -1 && botUpdates[botUpdatesId].waitedInput != null) {
                    text = $"{messageText} added to the list.";
                }
                else {
                    text = "Sorry, I can't understand what you are trying to do. Use my commands, please.";
                }
            }

            SendMessage(botClient, cancellationToken, chatId, text);
        }

        private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken) {
            var ErrorMessage = exception switch {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        private static string GetBotToken() {
            DotNetEnv.Env.TraversePath().Load();
            return Environment.GetEnvironmentVariable("BOT_TOKEN");
        }

        private static void StartBot(TelegramBotClient botClient, CancellationTokenSource cts) {
            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions {
                AllowedUpdates = Array.Empty<UpdateType>() // receives all update types
            };
            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token);
        }

        private static async void SendMessage(ITelegramBotClient botClient, CancellationToken cancellationToken, ChatId chatId, string text) {
            Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                cancellationToken: cancellationToken);
        }

        private static void WriteUpdate(long chatId, string waitedInput, string savedList) {
            var _botUpdate = new BotUpdate {
                chatId = chatId,
                waitedInput = waitedInput,
                savedList = savedList
            };

            botUpdates.Add(_botUpdate);

            var botUpdateString = JsonConvert.SerializeObject(botUpdates);

            System.IO.File.WriteAllText(fileName, botUpdateString);
        }

        private static int GetBotUpdatesId(long chatId) {
            int botUpdatesId = -1;

            var botUpdatesLength = botUpdates.Count();

            for (int i = 0; i < botUpdatesLength; i++) {
                if (botUpdates[i].chatId == chatId) {
                    botUpdatesId = i;
                }
            }

            return botUpdatesId;
        }
    }
}
