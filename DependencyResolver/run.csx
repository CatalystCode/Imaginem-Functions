#r "Newtonsoft.Json"

using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Formatting;
using System.Collections.Generic;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    string dependenciesStr = File.ReadAllText("dependencies.json");
    var dependencies = JsonConvert.DeserializeObject(dependenciesStr);
    var jsonDependencies = JObject.Parse(dependenciesStr);
    switch (req.Method.Method)
    {
        case "POST":
            try
            {
                var jsonFunctions = JArray.Parse(req.Content.ReadAsStringAsync().Result);
                var responseJson = new JArray();
                var dependencyList = new LinkedList<string>();
                foreach (var function in jsonFunctions)
                {
                    if (ResolveDependency(dependencyList, function.ToString(), jsonDependencies) == false)
                    {
                        return req.CreateResponse(HttpStatusCode.BadRequest, "unknown function specified in body");
                    }
                }
                foreach (var dependency in dependencyList)
                {
                    if (responseJson.FirstOrDefault(x => x.ToString() == dependency) == null)
                    {
                        responseJson.Add(dependency);
                    }
                }
                return req.CreateResponse(HttpStatusCode.OK, responseJson, JsonMediaTypeFormatter.DefaultMediaType);
            }
            catch (Exception ex)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "invalid json array specified in body");
            }
            break;
        case "GET":
            return req.CreateResponse(HttpStatusCode.OK, dependencies, JsonMediaTypeFormatter.DefaultMediaType);
    }
    return req.CreateResponse(HttpStatusCode.BadRequest, "HttpMethod not supported");
}

public static bool ResolveDependency(LinkedList<string> dependencyList, string function, JObject jsonDependencies)
{
    var dependingFunctions = jsonDependencies.GetValue(function);
    if (dependingFunctions == null)
    {
        return false;
    }
    dependencyList.AddFirst(function);
    foreach (var dependency in dependingFunctions)
    {
        dependencyList.AddFirst(dependency.ToString());
        ResolveDependency(dependencyList, dependency.ToString(), jsonDependencies);
    }
    return true;
}