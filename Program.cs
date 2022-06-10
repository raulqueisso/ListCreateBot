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
        public string commandWaitingForInput;
        public string savedList;
    }

    class Program {
        static BotUpdate botUpdate;

        static void Main(string[] args) {
            var botClient = new TelegramBotClient(GetBotToken());

            using var cts = new CancellationTokenSource();

            StartBot(botClient, cts);

            Console.WriteLine($"Start listening for messages.");
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

            if (System.IO.File.Exists(GetFileName(chatId))) {
                ReadUpdate(chatId);
            }

            string text;

            if (messageText == "/add") {
                text = "OK! Send one or multiple items separated by a comma. Like this:\n\nItem 1, item 2, item 3";

                WriteUpdate(chatId, "/add");
            } else {
                if (botUpdate.commandWaitingForInput != null) {
                    text = $"{messageText} added to the list.";

                    WriteUpdate(chatId, null, messageText);
                }
                else {
                    text = "Sorry, I can't understand what you are trying to do. Use my commands, please.";
                }
            }

            await SendMessage(botClient, cancellationToken, chatId, text); 
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

        private static async Task<Message> SendMessage(ITelegramBotClient botClient, CancellationToken cancellationToken, ChatId chatId, string text) {
            return await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                cancellationToken: cancellationToken);
        }

        private static void ReadUpdate(long chatId) {
            // Read all saved updates
            try {
                var fileName = GetFileName(chatId);
                var botUpdateString = System.IO.File.ReadAllText(fileName);
                    
                botUpdate = JsonConvert.DeserializeObject<BotUpdate>(botUpdateString);
            } catch (Exception ex) {
                Console.WriteLine($"Error reading or deserializing {ex}");
            }
        }

        private static void WriteUpdate(long chatId, string commandWaitingForInput, string savedList) {
            botUpdate = new BotUpdate {
                chatId = chatId,
                commandWaitingForInput = commandWaitingForInput,
                savedList = savedList
            };

            var botUpdateString = JsonConvert.SerializeObject(botUpdate);

            System.IO.File.WriteAllText(GetFileName(chatId), botUpdateString);
        }

        private static void WriteUpdate(long chatId, string commandWaitingForInput) {
            WriteUpdate(chatId, commandWaitingForInput, botUpdate.savedList);
        }

        private static string GetFileName(long chatId) {
            return $"updates/update_{chatId}.json";
        }
    }
}
