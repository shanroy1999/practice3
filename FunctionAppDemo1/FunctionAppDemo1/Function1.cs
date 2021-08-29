using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FunctionAppDemo1
{
    public static class Function1
    {
        // Name of the function
        [FunctionName("ABC")]
        public static async Task<IActionResult> Run(

            // HttpTrigger => trigger as a result of HTTP request
            // here -> trigger is defined for get and post requests
            // Authorization level is Anonymous => does not require any authentication
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // Information / Statement that will be shown in log when function called / executed
            log.LogInformation("C# HTTP trigger function processed a request. This is Shantanu.");

            // Function looks for name query parameter either in query string or in body of request.
            string name = req.Query["name"];

            // Read the requestBody till end asynchronously and return it as string
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Deserialize Json Object to .Net object
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            // if no name paramter specified  => Pass a name....
            // if name paramtere specified => Hello, name
            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
