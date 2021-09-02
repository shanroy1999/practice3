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

        static string queueConnection = "Endpoint=sb://shanbus.servicebus.windows.net/;SharedAccessKeyName=policy1;SharedAccessKey=05vDGWYEyLO0St4xcAvZIBY13yaZ5mSb3kO2uJnUJhA=;";
        static string queueName = "appqueue";

        static string topicConnection = "Endpoint=sb://shanbus.servicebus.windows.net/;SharedAccessKeyName=sendMessage;SharedAccessKey=HfnWz82NE+UNv7QbzCjPZePX/lKr8zzrAYW2YYWGzxc=;";
        static string topicName = "apptopic";
        static string subscriptionName = "S1";

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

            /*
            try
            {
                ThrowException();
            }
            catch (Exception e)
            {
                Telemetry.TrackException(e);
            }
            */

            // Function looks for name query parameter either in query string or in body of request.
            string name = req.Query["name"];

            // Read the requestBody till end asynchronously and return it as string
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Console.WriteLine(requestBody);

            // Input type from the user for where to send / receive message to / from
            string type = req.Query["type"];

            switch (type)
            {
                case "queueSend":
                    {

                        List<MessageContent> messagesList = new List<MessageContent>()
                        {
                            new MessageContent() { MessageId = "1", Content = "HTTP Request Successful" },
                            new MessageContent() { MessageId = "2", Content = "Thank you for the submission" },
                            new MessageContent() { MessageId = "3", Content = "We value our customers" },
                            new MessageContent() { MessageId = "4", Content = "Message Delivered Successfully" }
                        };

                        // ===================================================
                        // SEND MESSAGE TO SERVICE BUS QUEUE
                        // ===================================================

                        ServiceBusClient queueClient = new ServiceBusClient(queueConnection);
                        ServiceBusSender queueSender = queueClient.CreateSender(queueName);

                        Console.WriteLine("Queue Messages Sent : ");

                        foreach (MessageContent mContent in messagesList)
                        {
                            ServiceBusMessage serviceBusMsg = new ServiceBusMessage(mContent.ToString());
                            serviceBusMsg.ContentType = "application/json";
                            queueSender.SendMessageAsync(serviceBusMsg).GetAwaiter().GetResult();

                            log.LogInformation($"Message Body : {serviceBusMsg.Body.ToString()}");
                            Telemetry.TrackTrace($"Sending Message to the Queue {queueName}");
                            Telemetry.TrackEvent($"Message Body : {serviceBusMsg.Body.ToString()}");
                        }

                        await queueSender.DisposeAsync();

                        // ========================================================
                        // ADD TELEMETRY TO TRACK THE EVENTS FOR SERVICE BUS QUEUE
                        // =========================================================

                        // Check the number of messages in the queue
                        var management_client = new ManagementClient(queueConnection);
                        var queueInfo = await management_client.GetQueueRuntimeInfoAsync(queueName);
                        var messageCount = queueInfo.MessageCount;
                        var countMetric = new MetricTelemetry();
                        countMetric.Name = "queueLength";
                        countMetric.Sum = messageCount;
                        var countMetricInfo = new Dictionary<string, double>();
                        countMetricInfo.Add(countMetric.Name, countMetric.Sum);
                        Telemetry.TrackEvent($"Queue Length : {countMetric.Sum.ToString()}");

                        // Check the size of the queue in Bytes
                        var queueSize = queueInfo.SizeInBytes;
                        var sizeMetric = new MetricTelemetry();
                        sizeMetric.Name = "queueSize";
                        sizeMetric.Sum = queueSize;
                        var sizeMetricInfo = new Dictionary<string, double>();
                        sizeMetricInfo.Add(sizeMetric.Name, sizeMetric.Sum);
                        Telemetry.TrackEvent($"Queue Size : {sizeMetric.Sum.ToString()}");

                        string funcSuccessMessage = "Function Successfully Executed. Message recorded in Service Bus Queue";
                        log.LogInformation(funcSuccessMessage);
                        Telemetry.TrackEvent(funcSuccessMessage);

                        break;
                    }

                case "queueReceive":
                    {

                        // ========================================================
                        // RECEIVE MESSAGE FROM SERVICE BUS QUEUE
                        // ========================================================

                        // Create a receiver for the service bus
                        // Ensure that we only peek on the messages in the queue => ServiceBusReceiverOptions object
                        ServiceBusClient queueClient = new ServiceBusClient(queueConnection);
                        ServiceBusReceiver queueReceiver = queueClient.CreateReceiver(queueName,
                            new ServiceBusReceiverOptions()
                            {
                                // ReceiveMode = ServiceBusReceiveMode.PeekLock
                                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
                            });

                        // Create a received message from the receiver object
                        var messagesReceived = queueReceiver.ReceiveMessagesAsync(2).GetAwaiter().GetResult();

                        Console.WriteLine("Queue Messages Received : ");

                        // Write the received message body on the console
                        foreach (var message in messagesReceived)
                        {
                            log.LogInformation(message.SequenceNumber.ToString());
                            log.LogInformation(message.Body.ToString());
                            Telemetry.TrackTrace($"Receiving Message from the Queue {queueName}");
                        }

                        // Resource Cleanup
                        await queueClient.DisposeAsync();
                        await queueReceiver.DisposeAsync();

                        break;
                    }

                case "topicSend":
                    {

                        // ==============================================
                        // FOR SENDING TO TOPIC => PUBLISHING TO TOPIC
                        // ==============================================

                        // Creating Sender and Client for the Topic
                        ServiceBusClient topicClient = new ServiceBusClient(topicConnection);
                        ServiceBusSender topicSender = topicClient.CreateSender(topicName);

                        // /*
                        // Create a message batch to store messages
                        using ServiceBusMessageBatch messageBatch = await topicSender.CreateMessageBatchAsync();
                        int numOfMessages = 5;
                        for (int i = 1; i <= numOfMessages; i++)
                        {
                            Telemetry.TrackTrace($"Sending message to the Topic {topicName}");
                            messageBatch.TryAddMessage(new ServiceBusMessage($"Message number {i}"));
                        }

                        // Send the batch of messages to service bus topic asynchronously
                        await topicSender.SendMessagesAsync(messageBatch);
                        Console.WriteLine("Topic Messages Sent : ");
                        log.LogInformation($"Batch of {numOfMessages} messages has been published");

                        // Free the resources, perform cleanup
                        await topicClient.DisposeAsync();
                        await topicSender.DisposeAsync();

                        break;
                    }
                    
                case "topicReceive":
                    {
                        // ================================================
                        // RECEIVING MESSAGE FROM THE TOPIC => SUBSCRIPTION
                        // ================================================

                        
                        SubscriptionClient subscriptionClient = new SubscriptionClient(topicConnection,
                            topicName,
                            subscriptionName,
                            // ReceiveMode.PeekLock
                            ReceiveMode.ReceiveAndDelete
                            );
                        subscriptionClient.RegisterMessageHandler((subscriptionMessage, canceltoken) =>
                        {
                            var b = subscriptionMessage.Body;  // Gives Byte object
                            // Convert Byte Object to String
                            string subscriberMessage = System.Text.Encoding.UTF8.GetString(b);
                            Console.WriteLine("Message Received : " + subscriberMessage);
                            return Task.CompletedTask;
                        },
                        (exceptionArgs) =>
                        {
                            Console.WriteLine("Exception Occurred : " + exceptionArgs.Exception.ToString());
                            return Task.CompletedTask;
                        });
                        Telemetry.TrackTrace("Messages Successfully Received by Subscription from the Topic");
                        

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

        /*
        public void ThrowException()
        {
            throw new ApplicationException("Exception Testing");
        }
        */

    }
}