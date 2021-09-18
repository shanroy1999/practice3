using Azure.Messaging.ServiceBus;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

// namespace => used as a container to organize various classes
namespace FunctionAppDemo1
{
    public class Function1
    {

        // Replace log messages and call Telemetry class
        // Telemetry => means monitoring and analyzing information about system to track performance
        // TelemetryClient => helps in monitoring cutom events and track them
        TelemetryClient Telemetry
        {
            get;
        }

        // Dependency injection - allow creation of dependent objects outside class
        // Client Class (dependent) - depends on service class,
        // Service Class (dependency) - provide service to client
        // Injector Class - create service class object and injects into client class
        // Dependency Injection for correctly configuring Application Insights - Constructor Injection
        public Function1(TelemetryClient telemetry)
        {
            Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        // Defining the queue Connection String and the name taken from Azure Portal
        // static string queueConnection = "Endpoint=sb://shanbus.servicebus.windows.net/;SharedAccessKeyName=sendMessage;SharedAccessKey=LpfpuAzei9mGe/2BLdMvkoEsrH64xVOJkl5Vhxy8Aas=;";
        static string queueName = "functionqueue";

        // Defining the topic Connection String and the name taken from Azure Portal
        // static string topicConnection = "Endpoint=sb://shanbus.servicebus.windows.net/;SharedAccessKeyName=sendMessage;SharedAccessKey=aJoZA4IuaNpJEolnGV3v1pKKVteZ+wY/RWF7w0m/SsU=;";
        static string topicName = "apptopic";

        static string globalConnectionString = "Endpoint=sb://shanbus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=Z5u6EnhAVNLJzqWmlSvBCY+CUFZm5EJPDLVG/ocPKvA=";

        // defining the subscriptionName to be created for the topic
        static string subscriptionName = "S1";

        // Name of the function
        [FunctionName("ABC")]
        // [return: ServiceBus("appqueue", EntityType.Queue, Connection = "servicebus-connection")]

        // async & await => together enables asynchronous communication
        // async function => expects an await expression in return type, can contain 0 / more await expressions
        // async => returns Task<TResult> object
        // IActionResult => represents result of an Action method
        public async Task<IActionResult> Run(

            // HttpTrigger => trigger as a result of HTTP request
            // here -> trigger is defined for get and post requests
            // Authorization level is Anonymous => does not require any authentication
            // ILogger -> used to perform logging of messages
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // Information / Statement that will be shown in log when function called / executed
            // Send Trace message to check message progress status for display in Diagnostic Search
            Telemetry.TrackTrace("C# HTTP trigger function processed a request. This is Shantanu.");

            // Send information about the page viewed in the application to ApplicationInsights
            // PageViewTelemetry => Track Page Views
            // GetLeftPart => Get specified portion of Uri instance
            //             => includes scheme, authority, path and query
            //             => scheme -> https://,    authority -> https://localhost:8080/
            //             => path -> https://localhost:8080/ABC? ,  query ->  https://localhost:8080/ABC?query
            // UriPartial => specifies End of Uri Portion to return
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

            // Input type from the user for where to send / receive message to / from
            string type = req.Query["type"];

            switch (type)
            {
                case "queueSend":
                    {
                        // /*
                        // Create a list for Messages
                        List<MessageContent> messagesList = new List<MessageContent>()
                        {
                            // Create Class object
                            new MessageContent() { MessageId = "1", Content = "HTTP Request Successful" },
                            new MessageContent() { MessageId = "2", Content = "Thank you for the submission" },
                            new MessageContent() { MessageId = "3", Content = "We value our customers" },
                            new MessageContent() { MessageId = "4", Content = "Message Delivered Successfully" }
                        };

                        // ===================================================
                        // SEND MESSAGE TO SERVICE BUS QUEUE
                        // ===================================================

                        ServiceBusSender queueSender = InitiateQueueSender();

                        sendMessageToQueue(messagesList, queueSender);

                        await queueSender.DisposeAsync();
                        Telemetry.TrackEvent("All messages have been sent to the queue");

                        await addQueueTelemetry();
                        // */

                        /*
                        ServiceBusClient queueClient = new ServiceBusClient(globalConnectionString);
                        ServiceBusSender queueSender = queueClient.CreateSender(queueName);
                        using ServiceBusMessageBatch messageBatch = await queueSender.CreateMessageBatchAsync();
                        for(int i=0; i<5; i++)
                        {
                            Telemetry.TrackTrace($"Sending message to the Queue {queueName}");
                            if (!messageBatch.TryAddMessage(new ServiceBusMessage($"Message number {i}")))
                            {
                                throw new Exception($"The Queue message Message {i} is too large to fit in the batch.");
                            };
                        }
                        try
                        {
                            await queueSender.SendMessagesAsync(messageBatch);
                            Telemetry.TrackTrace($"A batch of messages has been published to the queue");
                        }
                        finally
                        {
                            await queueSender.DisposeAsync();
                            await queueClient.DisposeAsync();
                        }
                        */

                        break;
                    }

                case "queueReceive":
                    {
                        // ========================================================
                        // RECEIVE MESSAGE FROM SERVICE BUS QUEUE
                        // ========================================================

                        // Create a receiver for receiving from the Service Bus queue
                        // PeekLock - Retrieves and locks a message for processing,
                        //          - message cannot be received in lock duration
                        //          - when lock expires, message available to all other receivers
                        // ReceiveAndDelete - Deletes the messages as soon as they are received
                        // ServiceBusReceiverOptions - configures the behaviour of ServiceBusReceiver



                        ServiceBusClient queueClient;
                        ServiceBusReceiver queueReceiver;
                        InitiateQueueReceiver(out queueClient, out queueReceiver);

                        IReadOnlyList<ServiceBusReceivedMessage> messagesReceived = receiveQueueMessages(queueReceiver);

                        // Write the received message body on the console
                        foreach (var message in messagesReceived)
                        {
                            log.LogInformation($"Message Sequence : {message.SequenceNumber}");
                            log.LogInformation($"Message Body : {message.Body}");
                            Telemetry.TrackEvent($"Receiving Message from the Queue {queueName}");
                        }

                        await queueClient.DisposeAsync();
                        await queueReceiver.DisposeAsync();

                        Telemetry.TrackTrace("Messages from Queue successfully received.");

                        break;
                    }

                case "topicSend":
                    {
                        // ==============================================
                        // FOR SENDING TO TOPIC => PUBLISHING TO TOPIC
                        // ==============================================

                        // Creating Sender and Client for the Topic
                        ServiceBusSender topicSender = InitiateTopicSender();

                        // /*
                        // Create a message batch to store messages
                        ServiceBusMessageBatch messageBatch = await InitiateMessageBatch(topicSender);
                        int numOfMessages = 5;
                        await SendMessageToTopic(topicSender, messageBatch, numOfMessages);

                        Telemetry.TrackTrace("All Messages have been sent to the topic");
                        Telemetry.TrackTrace($"Batch of {numOfMessages} messages has been published to the topic");

                        await topicSender.DisposeAsync();

                        break;
                    }

                case "topicReceive":
                    {
                        // ================================================
                        // RECEIVING MESSAGE FROM THE TOPIC => SUBSCRIPTION
                        // ================================================

                        ServiceBusClient topicClient;
                        ServiceBusReceiver topicReceiver;

                        InitiateTopicReceiver(out topicClient, out topicReceiver);
                        ReceiveMessagesFromTopic(log, topicReceiver, 5);

                        await topicClient.DisposeAsync();
                        await topicReceiver.DisposeAsync();

                        /*
                        // Create a Subscription Client for receiving messages from topic
                        SubscriptionClient subscriptionClient = new SubscriptionClient(
                            topicConnection,
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
                            Telemetry.TrackTrace("Message Received : " + subscriberMessage);
                            // return the task that has been completed successfully
                            return Task.CompletedTask;
                        },
                        (exceptionArgs) =>
                        {
                            Telemetry.TrackTrace("Exception Occurred : " + exceptionArgs.Exception.ToString());
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

            return await createCustomResponse(req);
        }

        private async Task SendMessageToTopic(ServiceBusSender topicSender, ServiceBusMessageBatch messageBatch, int numOfMessages)
        {
            for (int i = 1; i <= numOfMessages; i++)
            {
                Telemetry.TrackTrace($"Sending message to the Topic {topicName}");

                // TryAddMessage => tries adding message to the batch ensuring size of batch <= max
                messageBatch.TryAddMessage(new ServiceBusMessage($"Message number {i}"));
            }

            // Send the batch of messages to service bus topic asynchronously
            await topicSender.SendMessagesAsync(messageBatch);
        }

        private static async Task<ServiceBusMessageBatch> InitiateMessageBatch(ServiceBusSender topicSender)
        {
            return await topicSender.CreateMessageBatchAsync();
        }

        private async Task<IActionResult> createCustomResponse(HttpRequest req)
        {
            string name = req.Query["name"];

            // Read the requestBody till end and return it as string => Asynchronous read operation
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Console.WriteLine(requestBody);

            // Deserialize Json Object to .Net object
            // Deserialization - takes data from file and builds it into an object
            // dynamic - avoids compile-time checking
            // compiler - compiles dynamic types into object types mostly

            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name ??= data?.name;

            // Function looks for name query parameter either in query string or in body of request.
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

        private void ReceiveMessagesFromTopic(ILogger log, ServiceBusReceiver topicReceiver, int numOfMessages)
        {
            var messages_received = topicReceiver.ReceiveMessagesAsync(numOfMessages).GetAwaiter().GetResult();

            foreach (var message in messages_received)
            {
                // log.LogInformation(message.SequenceNumber.ToString());
                // log.LogInformation(message.Body.ToString());
                Telemetry.TrackTrace($"Receiving messages from topic {topicName}");
                Telemetry.TrackTrace($"Message Sequence {message.SequenceNumber} : {message.Body.ToString()}");
            }

            log.LogInformation("Topic Messages Received Successfully");
        }

        private static ServiceBusSender InitiateTopicSender()
        {
            ServiceBusClient topicClient = new ServiceBusClient(globalConnectionString);
            ServiceBusSender topicSender = topicClient.CreateSender(topicName);
            return topicSender;
        }

        private static void InitiateTopicReceiver(out ServiceBusClient topicClient, out ServiceBusReceiver topicReceiver)
        {
            topicClient = new ServiceBusClient(globalConnectionString);
            topicReceiver = topicClient.CreateReceiver(
                topicName, subscriptionName, new ServiceBusReceiverOptions()
                {
                    ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
                });
        }

        private static IReadOnlyList<ServiceBusReceivedMessage> receiveQueueMessages(ServiceBusReceiver queueReceiver)
        {
            // Receive a ServiceBusReceivedMessage object from the receiver object
            // in the configured received mode.
            return queueReceiver.ReceiveMessagesAsync(2).GetAwaiter().GetResult();
        }

        private static void InitiateQueueReceiver(out ServiceBusClient queueClient, out ServiceBusReceiver queueReceiver)
        {
            queueClient = new ServiceBusClient(globalConnectionString);
            queueReceiver = queueClient.CreateReceiver(queueName,
                new ServiceBusReceiverOptions()
                {
                    // ReceiveMode = ServiceBusReceiveMode.PeekLock
                    ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
                });
        }

        private async Task addQueueTelemetry()
        {
            // ========================================================
            // ADD TELEMETRY TO TRACK THE EVENTS FOR SERVICE BUS QUEUE
            // =========================================================

            // Check the number of messages in the queue
            var managementClient = new ManagementClient(globalConnectionString);
            var queueInfo = await managementClient.GetQueueRuntimeInfoAsync(queueName);
            var messageCount = queueInfo.MessageCount;
            var countMetric = new MetricTelemetry();
            countMetric.Name = "queueLength";
            countMetric.Sum = messageCount;
            Telemetry.TrackEvent($"Queue Length : {countMetric.Sum.ToString()}");

            // Check the size of the queue in Bytes
            var queueSize = queueInfo.SizeInBytes;
            var sizeMetric = new MetricTelemetry();
            sizeMetric.Name = "queueSize";
            sizeMetric.Sum = queueSize;
            Telemetry.TrackEvent($"Queue Size : {sizeMetric.Sum.ToString()}");

            string funcSuccessMessage = "Function Successfully Executed. Message recorded in Service Bus Queue";
            // log.LogInformation(funcSuccessMessage);
            Telemetry.TrackEvent(funcSuccessMessage);
        }

        private void sendMessageToQueue(List<MessageContent> messagesList, ServiceBusSender queueSender)
        {
            foreach (MessageContent m in messagesList)
            {
                // Convert the MessageContent object to ServiceBusMessage
                ServiceBusMessage serviceBusMsg = new ServiceBusMessage(m.ToString());

                // set the content type of the service bus message
                //
                serviceBusMsg.ContentType = "application/json";
                //

                // Add a new message at the back of the queue
                queueSender.SendMessageAsync(serviceBusMsg).GetAwaiter().GetResult();

                // log.LogInformation($"Sending Message to the Queue {queueName}");
                Telemetry.TrackTrace($"Sending Message to the Queue {queueName}");

                // log.LogInformation($"Message Body : {serviceBusMsg.Body.ToString()}");
                Telemetry.TrackTrace($"Message Body : {serviceBusMsg.Body}");
            }
        }

        private static ServiceBusSender InitiateQueueSender()
        {
            // create Service Bus Client for sending and processing of messages
            ServiceBusClient queueClient = new ServiceBusClient(globalConnectionString);

            // Create sender to send the message to the queue
            ServiceBusSender queueSender = queueClient.CreateSender(queueName);
            return queueSender;
        }


        /*
        public void ThrowException()
        {
            throw new ApplicationException("Exception Testing");
        }
        */

    }
}