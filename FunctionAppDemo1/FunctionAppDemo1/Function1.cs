using Azure.Messaging.ServiceBus;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FunctionAppDemo1
{
    public class Function1
    {

        // Replace log messages and call Telemetry class
        TelemetryClient Telemetry
        {
            get;
        }

        // Dependency Injection for correctly configuring Application Insights
        public Function1(TelemetryClient telemetry)
        {
            Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        static string queue_connection = "Endpoint=sb://shanbus.servicebus.windows.net/;SharedAccessKeyName=policy1;SharedAccessKey=05vDGWYEyLO0St4xcAvZIBY13yaZ5mSb3kO2uJnUJhA=;";
        static string queue_name = "appqueue";

        static string topic_connection = "Endpoint=sb://shanbus.servicebus.windows.net/;SharedAccessKeyName=sendMessage;SharedAccessKey=HfnWz82NE+UNv7QbzCjPZePX/lKr8zzrAYW2YYWGzxc=;";
        static string topic_name = "apptopic";
        static string subscription_name = "S1";

        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Name of the function
        [FunctionName("ABC")]
        // [return: ServiceBus("appqueue", EntityType.Queue, Connection = "servicebus-connection")]
        public async Task<IActionResult> Run(

            // HttpTrigger => trigger as a result of HTTP request
            // here -> trigger is defined for get and post requests
            // Authorization level is Anonymous => does not require any authentication
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // Information / Statement that will be shown in log when function called / executed
            // Send Trace message for display in Diagnostic Search
            Telemetry.TrackTrace("C# HTTP trigger function processed a request. This is Shantanu.");

            // Send information about the page viewed in the application to ApplicationInsights
            // PageViewTelemetry => Track Page Views
            Telemetry.TrackPageView(new PageViewTelemetry
            {
                Name = "ABC",
                Url = new Uri(req.GetUri().GetLeftPart(UriPartial.Path)),
                Timestamp = DateTime.UtcNow
            });

            try
            {
                ThrowException();
            }
            catch (Exception e)
            {
                Telemetry.TrackException(e);
            }

            // Function looks for name query parameter either in query string or in body of request.
            string name = req.Query["name"];

            // Read the requestBody till end asynchronously and return it as string
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Console.WriteLine(requestBody);

            // Input type from the user for where to send / receive message to / from
            string type = req.Query["type"];

            switch (type)
            {
                case "queue":
                    {

                        List<MessageContent> messages = new List<MessageContent>()
                        {
                            new MessageContent() { MessageId = "1", Content = "HTTP Request Successful" },
                            new MessageContent() { MessageId = "2", Content = "Thank you for the submission" },
                            new MessageContent() { MessageId = "3", Content = "We value our customers" },
                            new MessageContent() { MessageId = "4", Content = "Message Delivered Successfully" }
                        };

                        // ===================================================
                        // SEND MESSAGE TO SERVICE BUS QUEUE
                        // ===================================================

                        ServiceBusClient client = new ServiceBusClient(queue_connection);
                        ServiceBusSender sender = client.CreateSender(queue_name);

                        Console.WriteLine("Queue Messages Sent : ");

                        foreach (MessageContent m in messages)
                        {
                            ServiceBusMessage ms = new ServiceBusMessage(m.ToString());
                            ms.ContentType = "application/json";
                            sender.SendMessageAsync(ms).GetAwaiter().GetResult();

                            log.LogInformation($"Message Body : {ms.Body.ToString()}");
                            Telemetry.TrackTrace($"Sending Message to the Queue {queue_name}");
                            Telemetry.TrackEvent($"Message Body : {ms.Body.ToString()}");
                        }

                        await sender.DisposeAsync();

                        // ========================================================
                        // ADD TELEMETRY TO TRACK THE EVENTS FOR SERVICE BUS QUEUE
                        // =========================================================

                        // Check the number of messages in the queue
                        var management_client = new ManagementClient(queue_connection);
                        var queue = await management_client.GetQueueRuntimeInfoAsync(queue_name);
                        var messageCount = queue.MessageCount;
                        var sample = new MetricTelemetry();
                        sample.Name = "queueLength";
                        sample.Sum = messageCount;
                        var dict = new Dictionary<string, double>();
                        dict.Add(sample.Name, sample.Sum);
                        Telemetry.TrackEvent($"Queue Length : {sample.Sum.ToString()}");

                        // Check the size of the queue in Bytes
                        var queueSize = queue.SizeInBytes;
                        var sample2 = new MetricTelemetry();
                        sample2.Name = "queueSize";
                        sample2.Sum = queueSize;
                        var dict2 = new Dictionary<string, double>();
                        dict.Add(sample2.Name, sample2.Sum);
                        Telemetry.TrackEvent($"Queue Size : {sample2.Sum.ToString()}");

                        string msg = "Function Successfully Executed. Message recorded in Service Bus Queue";
                        log.LogInformation(msg);
                        Telemetry.TrackEvent(msg);

                        // ========================================================
                        // RECEIVE MESSAGE FROM SERVICE BUS QUEUE
                        // ========================================================

                        // Create a receiver for the service bus
                        // Ensure that we only peek on the messages in the queue => ServiceBusReceiverOptions object
                        ServiceBusReceiver receiver = client.CreateReceiver(queue_name,
                            new ServiceBusReceiverOptions()
                            {
                                // ReceiveMode = ServiceBusReceiveMode.PeekLock
                                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
                            });

                        // Create a received message from the receiver object
                        var messages_received = receiver.ReceiveMessagesAsync(2).GetAwaiter().GetResult();

                        Console.WriteLine("Queue Messages Received : ");

                        // Write the received message body on the console
                        foreach (var message in messages_received)
                        {
                            log.LogInformation(message.SequenceNumber.ToString());
                            log.LogInformation(message.Body.ToString());
                            Telemetry.TrackTrace($"Receiving Message from the Queue {queue_name}");
                        }

                        // Resource Cleanup
                        await client.DisposeAsync();
                        await receiver.DisposeAsync();

                        break;
                    }

                case "topic":
                    {

                        // ==============================================
                        // FOR SENDING TO TOPIC => PUBLISHING TO TOPIC
                        // ==============================================

                        // Creating Sender and Client for the Topic
                        ServiceBusClient client2 = new ServiceBusClient(topic_connection);
                        ServiceBusSender sender2 = client2.CreateSender(topic_name);

                        // /*
                        // Create a message batch to store messages
                        using ServiceBusMessageBatch messageBatch = await sender2.CreateMessageBatchAsync();
                        int nMessage = 5;
                        for (int i = 1; i <= nMessage; i++)
                        {
                            Telemetry.TrackTrace($"Sending message to the Topic {topic_name}");
                            messageBatch.TryAddMessage(new ServiceBusMessage($"Message number {i}"));
                        }

                        // Send the batch of messages to service bus topic asynchronously
                        await sender2.SendMessagesAsync(messageBatch);
                        Console.WriteLine("Topic Messages Sent : ");
                        log.LogInformation($"Batch of {nMessage} messages has been published");

                        // Free the resources, perform cleanup
                        
                        await sender2.DisposeAsync();
                        // */

                        // ================================================
                        // RECEIVING MESSAGE FROM THE TOPIC => SUBSCRIPTION
                        // ================================================

                        /*
                        SubscriptionClient subscriptionClient = new SubscriptionClient(topic_connection,
                            topic_name,
                            subscription_name,
                            // ReceiveMode.PeekLock
                            ReceiveMode.ReceiveAndDelete
                            );

                        subscriptionClient.RegisterMessageHandler((message, canceltoken) =>
                        {
                            var b = message.Body;  // Gives Byte object

                            // Convert Byte Object to String
                            string ms = System.Text.Encoding.UTF8.GetString(b);

                            Console.WriteLine("Message Received : " + ms);
                            return Task.CompletedTask;
                        },
                        (exceptionArgs) =>
                        {
                            Console.WriteLine("Exception Occurred : " + exceptionArgs.Exception.ToString());
                            return Task.CompletedTask;
                        });

                        Telemetry.TrackTrace("Messages Successfully Received by Subscription from the Topic");
                        */

                        break;

                    }
                default:
                    {
                        Telemetry.TrackTrace("No Type has been given for sending / receiving.");
                        break;
                    }
            }

            // Deserialize Json Object to .Net object
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name ??= data?.name;

            if (string.IsNullOrEmpty(name))
            {
                string responseMessage = "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.";

                // When the asynchronous operation completes,
                // the await operator returns the result of the operation, if any
                return await Task.FromResult(new OkObjectResult(responseMessage));
            }
            else if (name.Equals("Ronaldo"))
            {
                var dictionary = new Dictionary<string, string>();
                dictionary.Add("nameParameter", name);
                Telemetry.TrackEvent("Ronaldo is best", dictionary);
                Telemetry.TrackEvent("Ronaldo passed as an argument");
                string responseMessage = $"Hello, {name}, You are the best. This HTTP triggered function executed successfully.";

                return await Task.FromResult(new OkObjectResult(responseMessage));
            }
            else
            {
                var dictionary = new Dictionary<string, string>();
                dictionary.Add("nameParameter", name);
                Telemetry.TrackEvent("This is not Ronaldo", dictionary);
                Telemetry.TrackEvent("Ronaldo not passed as an argument");
                string responseMessage = $"Hello, {name}. This HTTP triggered function executed successfully.";
                return await Task.FromResult(new OkObjectResult(responseMessage));
            }
        }

        public void ThrowException()
        {
            throw new ApplicationException("Exception Testing");
        }

    }
}
