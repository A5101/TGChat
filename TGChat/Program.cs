using System;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TGBot
{
    // класс сообщения
    class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    class Request
    {
        [JsonPropertyName("model")]
        public string ModelId { get; set; } = "";
        [JsonPropertyName("messages")]
        public List<Message> Messages { get; set; } = new();
    }

    class ResponseData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("object")]
        public string Object { get; set; } = "";
        [JsonPropertyName("created")]
        public ulong Created { get; set; }
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = new();
        [JsonPropertyName("usage")]
        public Usage Usage { get; set; } = new();
    }

    class Choice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        [JsonPropertyName("message")]
        public Message Message { get; set; } = new();
        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; } = "";
    }

    class Usage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    class BotEngine
    {
        private readonly TelegramBotClient _botClient;
        private List<Message> messages;
        HttpClient httpClient;
        string endpoint;

        public BotEngine(TelegramBotClient botClient, string apiKey, string endpoint)
        {
            _botClient = botClient;
            this.endpoint = endpoint;
            messages = new List<Message>();
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message)
                return;

            if (message.Text is not { } messageText)
                return;
            if (message.Text == "Clear")
            {
                messages.Clear(); 
                return;
            }

            string content = message.Text;

            // если введенное сообщение имеет длину меньше 1 символа
            // то выходим из цикла и завершаем программу
            if (content is not { Length: > 0 }) return;
            // формируем отправляемое сообщение
            var mes = new Message() { Role = "user", Content = content };
            // добавляем сообщение в список сообщений
            messages.Add(mes);

            // формируем отправляемые данные
            var requestData = new Request()
            {
                ModelId = "gpt-3.5-turbo",
                Messages = messages
            };
            // отправляем запрос
            using var response = await httpClient.PostAsJsonAsync(endpoint, requestData);

            // если произошла ошибка, выводим сообщение об ошибке на консоль
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"{(int)response.StatusCode} {response.StatusCode}");
                return;
            }
            // получаем данные ответа
            ResponseData? responseData = await response.Content.ReadFromJsonAsync<ResponseData>();

            var choices = responseData?.Choices ?? new List<Choice>();
            if (choices.Count == 0)
            {
                Console.WriteLine("No choices were returned by the API");
                return;
            }
            var choice = choices[0];
            var responseMessage = choice.Message;
            // добавляем полученное сообщение в список сообщений
            messages.Add(responseMessage);
            var responseText = responseMessage.Content.Trim();

            var chatId = message.Chat.Id;

            var sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: responseText + "\n" + responseData?.Usage.TotalTokens,
                cancellationToken: cancellationToken);
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public async Task ListenForMessagesAsync()
        {
            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );
            Console.ReadLine();
        }
    }

    class Program
    {
        [Obsolete]
        public async static Task Main()
        {
            string apiKey = "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";
            string endpoint = "https://api.openai.com/v1/chat/completions";
            var botClient = new BotEngine(new TelegramBotClient("6102076369:AAF5B9cvL45Hwow0Pc6APJaqjK7KxTXTxpE"), apiKey, endpoint);


            botClient.ListenForMessagesAsync();
        }
    }

}