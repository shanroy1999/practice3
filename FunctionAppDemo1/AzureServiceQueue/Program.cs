using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Generic;

namespace AzureServiceQueue
{
    class Program
    {
        // Add 2 properties : connection string and queue name
        private static string connection_string = "Endpoint=sb://shanbus.servicebus.windows.net/;SharedAccessKeyName=SendMessage;SharedAccessKey=L67apJ4lkRqU6tHojTTqgpgsYP+D60ecMOX4X/3iTG4=;EntityPath=appqueue2";
        public static string queue_name = "appqueue2";

        static void Main(string[] args)
        {

            // Create a list of new order and add these orders as messages on the queue
            List<Order> orders = new List<Order>()
            {
                new Order() { orderID = "01", quantity = 10, unitPrice = 9.99m },
                new Order() { orderID = "02", quantity = 15, unitPrice = 10.99m },
                new Order() { orderID = "03", quantity = 20, unitPrice = 11.99m },
                new Order() { orderID = "04", quantity = 25, unitPrice = 12.99m },
                new Order() { orderID = "05", quantity = 30, unitPrice = 13.99m }
            };

            // ServiceBusClient => allows to interact with ServiceBus
            ServiceBusClient client = new ServiceBusClient(connection_string);

            // ServiceBusSender => allows to send messages on to the specific queue
            // CreateSender => creates ServiceBusSender Instance
            ServiceBusSender sender = client.CreateSender(queue_name);

            // Loop through each of the order in the orders list
            foreach(Order order in orders)
            {
                // Create a Message => Entire JSON string sent as a message
                ServiceBusMessage message = new ServiceBusMessage(order.ToString());

                // Adds a new message to the back of a queue.
                sender.SendMessageAsync(message).GetAwaiter().GetResult();
            }

            Console.WriteLine("All messages have been sent");
        }
    }
}
