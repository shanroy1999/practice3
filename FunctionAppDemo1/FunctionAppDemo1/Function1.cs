using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;

namespace FunctionAppDemo1
{
    public class Function1
    {

        // Replace log messages and call Telemetry class
        TelemetryClient Telemetry
        {
            get;
        }

        public Function1(TelemetryClient telemetry)
        {
            Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        // Name of the function
        [FunctionName("ABC")]
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

            // Send information about the page viewed in the application
            Telemetry.TrackPageView(new PageViewTelemetry
            {
                Name = "ABC",
                Url = new Uri(req.GetUri().GetLeftPart(UriPartial.Path)),
                Timestamp = DateTime.UtcNow
            });

            try
            {
                ThrowException();
            } catch(Exception e)
            {
                Telemetry.TrackException(e);
            }


            // Function looks for name query parameter either in query string or in body of request.
            string name = req.Query["name"];

            // Read the requestBody till end asynchronously and return it as string
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Deserialize Json Object to .Net object
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name ??= data?.name;

            // if no name paramter specified  => Pass a name....
            // if name paramtere specified => Hello, name
            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return await Task.FromResult(new OkObjectResult(responseMessage));
        }

        public void ThrowException()
        {
            throw new ApplicationException("Exception Testing");
        }
    }
}
