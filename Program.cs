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
            var chatType = update.Message.Chat.Type;
            var messageText = update.Message.Text;

            Console.WriteLine($"Received a '{messageText}' message in chat {chatId}. {update.Message.Chat.Username}");


            if (System.IO.File.Exists(GetFileName(chatId))) {
                ReadBotData(chatId);
            }

            var text = "";

            switch (messageText) {
                // Show user list command
                case "/mylist":
                    text = GetList();
                    break;

                // Add items command
                case "/add":
                    if (chatType != ChatType.Private) {
                        text = "To add items in this chat's list, write one or multiple items separated by a comma after the command. Like this:\n\n/add item 1, item 2, item 3";
                    }
                    else {
                        text = "OK! To add items, send one or multiple items separated by a comma. Like this:\n\nItem 1, item 2, item 3";

                        WriteBotData(chatId, "/add");
                    }
                    break;
                
                // Remove items command
                case "/remove":
                    if (chatType != ChatType.Private) {
                        text = "To add items in this chat's list, write one or multiple items separated by a comma after the command. Like this:\n\n/add item 1, item 2, item 3";
                    }
                    else {
                        text = "OK! To remove items, send one or multiple items separated by a comma. Like this:\n\nItem 1, item 2, item 3";

                        WriteBotData(chatId, "/remove");
                    }
                    break;
                
                // Sort list alphabetically
                case "/sort":
                    botData.savedList.Sort();
                    WriteBotData(chatId, null, botData.savedList);
                    text = GetList();
                    break;

                // Erase the whole list
                case "/clean":
                    WriteBotData(chatId, null, null);
                    text = "Your list is empty now.\nUse command /add to add items to your list.";
                    break;

                // Every other case
                default:
                    // Bot is expecting for items to be added or removed
                    if (botData.commandWaitingForInput != null) {
                        EnsureSavedListExists();

                        var newItems = StringToList(messageText);

                        // Add items
                        if (botData.commandWaitingForInput == "/add") {
                            text = AddItems(chatId, newItems);
                        }

                        // Remove items
                        if (botData.commandWaitingForInput == "/remove") {
                            text = RemoveItems(botClient, cancellationToken, chatId, newItems).Result;
                        }
                    }
                    else {
                        if (messageText.StartsWith("/add")) { // Add items in just one line
                            var newItems = StringToList(messageText.Remove(0, 5));
                            text = AddItems(chatId, newItems);
                        }
                        else if (messageText.StartsWith("/remove")) { // Remove items in just one line
                            var newItems = StringToList(messageText.Remove(0, 8));
                            text = RemoveItems(botClient, cancellationToken, chatId, newItems).Result;
                        }
                        else { // Bot can't understand user interaction
                            text = "Sorry, I can't understand what you are trying to do. Use my commands, please.";
                        }
                    }
                    break;
            }

            // Send message
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

            return list;
        }

        private static string AddItems(long chatId, List<string> items) {
            EnsureSavedListExists();

            foreach (var item in items) {
                botData.savedList.Add(item.Replace(item[0], Char.ToUpper(item[0])));
            }
            WriteBotData(chatId, null, botData.savedList);

            return $"\"{String.Join(", ", items)}\" added to the list.";
        }

        private static async Task<string> RemoveItems(ITelegramBotClient botClient, CancellationToken cancellationToken, long chatId, List<string> items) {
            var itemsRemoved = new List<string>();
            var removedItem = false;
            EnsureSavedListExists();

            foreach (var item in items) {
                var itemIndex = GetItemIndex(item);

                if (itemIndex != -1) {
                    botData.savedList.RemoveAt(itemIndex);
                    itemsRemoved.Add(item);
                    removedItem = true;
                }
                else {
                    await SendMessage(botClient, cancellationToken, chatId, $"There is no \"{item}\" in the list.");
                }
            }

            WriteBotData(chatId, null, botData.savedList);

            if (removedItem) {
                return $"\"{String.Join(", ", itemsRemoved)}\" removed from the list.";
            }

            return "";
        }

        private static string GetList() {
            var text = "";

            if (botData.savedList == null || botData.savedList.Count == 0) {
                text = "Your list is empty.\nUse command /add to add items to your list.";
            }
            else {
                foreach (var item in botData.savedList) {
                    text += $"• {item}\n";
                }
            }

            return text;
        }

        private static void EnsureSavedListExists() {
            if (botData.savedList == null) {
                botData.savedList = new List<string>();
            }
        }

        private static int GetItemIndex(string item) {
            var list = new List<string>(botData.savedList);

            for (var i = 0; i < list.Count(); i++) {
                list[i] = list[i].ToLower();
            }

            return list.IndexOf(item.ToLower());
        }
    }
}
