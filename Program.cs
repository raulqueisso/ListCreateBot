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
    struct BotData {
        public long chatId;
        public string commandWaitingForInput;
        public List<string> savedList;
    }

    class Program {
        static BotData botData;

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

            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}. {update.Message.Chat.Username}");


            if (System.IO.File.Exists(GetFileName(chatId))) {
                ReadBotData(chatId);
            }

            var text = "";

            // Add items command
            if (messageText == "/add") {
                text = "OK! To add items, send one or multiple items separated by a comma. Like this:\n\nItem 1, item 2, item 3";

                WriteBotData(chatId, "/add");
            }
            // Show user list command
            else if (messageText == "/mylist") {
                if (botData.savedList == null) {
                    text = "Your list is empty.\nUse command /add to add itens to your list.";
                } else {
                    foreach (var item in botData.savedList) {
                        text += $"• {item}\n";
                    }
                }
            }
            // Remove items command
            else if (messageText == "/remove") {
                text = "OK! To remove, items send one or multiple items separated by a comma. Like this:\n\nItem 1, item 2, item 3";

                WriteBotData(chatId, "/remove");
            }
            else {
                // Items to be added or removed
                if (botData.commandWaitingForInput != null) {
                    if (botData.savedList == null) {
                        botData.savedList = new List<string>();
                    }

                    var new_items = StringToList(messageText);

                    if (botData.commandWaitingForInput == "/add") {
                        text = $"{messageText} added to the list.";

                        foreach (var item in new_items) {
                            botData.savedList.Add(item);
                        }
                    }
                    else if (botData.commandWaitingForInput == "/remove") {
                        text = $"{messageText} removed from the list.";

                        foreach (var item in new_items) {
                            botData.savedList.Remove(item);
                        }
                    }

                    WriteBotData(chatId, null, botData.savedList);
                }
                // Bot can't understand user interaction
                else {
                    text = "Sorry, I can't understand what you are trying to do. Use my commands, please.";
                }
            }

            if (text != "") {
                await SendMessage(botClient, cancellationToken, chatId, text); 
            }
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

        private static void ReadBotData(long chatId) {
            // Read bot saved data
            try {
                var fileName = GetFileName(chatId);
                var botDataString = System.IO.File.ReadAllText(fileName);
                    
                botData = JsonConvert.DeserializeObject<BotData>(botDataString);
            } catch (Exception ex) {
                Console.WriteLine($"Error reading or deserializing {ex}");
            }
        }

        private static void WriteBotData(long chatId, string commandWaitingForInput, List<string> savedList) {
            botData = new BotData {
                chatId = chatId,
                commandWaitingForInput = commandWaitingForInput,
                savedList = savedList
            };

            var botDataString = JsonConvert.SerializeObject(botData);

            System.IO.File.WriteAllText(GetFileName(chatId), botDataString);
        }

        private static void WriteBotData(long chatId, string commandWaitingForInput) {
            WriteBotData(chatId, commandWaitingForInput, botData.savedList);
        }

        private static string GetFileName(long chatId) {
            return $"botData/botData_{chatId}.json";
        }

        private static List<string> StringToList(string listString) {
            List<string> list = new List<string>();
            list = listString.Split(", ").ToList();

            foreach (var l in list) {
                Console.WriteLine(l);
            }

            return list;
        }
    }
}
