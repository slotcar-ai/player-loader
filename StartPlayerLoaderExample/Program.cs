using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;

namespace StartPlayerLoaderExample
{
    class Program
    {   
        static IQueueClient queueClient;

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var serviceBusConnectionString = Environment.GetEnvironmentVariable("SLOTCAR_AI_SERVICEBUS_KEY_SENDLISTEN");
            var queueName = "player-loader";
            queueClient = new QueueClient(serviceBusConnectionString, queueName);

            Console.WriteLine("======================================================");
            Console.WriteLine("Press ENTER key to exit after sending all the messages.");
            Console.WriteLine("======================================================");

            // Send messages.
            Console.WriteLine($"Sending message...");
            
            await SendMessageAsync("Hei igjen!");

            Console.WriteLine($"Message sent!");

            Console.ReadKey();

            await queueClient.CloseAsync();
        }

        static async Task SendMessageAsync(string messageBody)
        {
            var message = new Message(Encoding.UTF8.GetBytes(messageBody));
            try
            {
                await queueClient.SendAsync(message);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
            }
        }
    }
}
