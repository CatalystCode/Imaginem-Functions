#r "Newtonsoft.Json"

using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Formatting;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"C# HTTP trigger function processed a request. RequestUri={req.RequestUri}");
    string dependenciesStr = File.ReadAllText("dependencies.json");
    var dependencies = JsonConvert.DeserializeObject(dependenciesStr);
    var jsonDependencies = JObject.Parse(dependenciesStr);
    switch (req.Method.Method)
    {
        case "POST":
            var jsonFunctions = JArray.Parse(req.Content.ReadAsStringAsync().Result);
            var responseJson = new JArray();
            foreach (var function in jsonFunctions)
            {
                foreach (var dependency in jsonDependencies.GetValue(function.ToString()))
                {
                    responseJson.Add(dependency.ToString());
                }
            }
            return req.CreateResponse(HttpStatusCode.OK, responseJson, JsonMediaTypeFormatter.DefaultMediaType);
            break;
        case "GET":
            return req.CreateResponse(HttpStatusCode.OK, dependencies, JsonMediaTypeFormatter.DefaultMediaType);
    }
    return req.CreateResponse(HttpStatusCode.BadRequest, "Test");
}