#load "..\CSharpScripting.csx"
#load "..\EnumerableExtensions.csx"

#r "System.Configuration"
#r "System.Collections"
#r "System.Runtime"
#r "System.Web"
#r "MyExtensions.dll"

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using MyExtensions;

private const string TRIGGER_WORD = "@C#:";
private static string _slackWebhookUrl = ConfigurationManager.AppSettings["SlackIncomingWebhookUrl"];

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Verbose("Outgoding webhook Charp Compiler service was triggered!");

    var content = await req.Content.ReadAsStringAsync();
    log.Verbose(content);

    var data = content
        .Split('&')
        .Select(x => x.Split('='))
        .ToDictionary(x => x[0], x => HttpUtility.HtmlDecode(HttpUtility.UrlDecode(x[1])));

    if (data["user_name"] == "slackbot")
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, new {
            body = "Cannot Support Messages From SlackBot.",
        });
    }

    var text = data["text"] as string ?? "";
    log.Verbose(text);

    var code = text.Replace(TRIGGER_WORD, "");

    // Evaluate C# Code with Roslyn
    log.Verbose($"{nameof(code)} : {code}");
    var resultText = await EvaluateCSharpAsync(code);
    log.Verbose(resultText);

    // Send back with Slack Incoming Webhook
    var message = string.IsNullOrWhiteSpace(resultText) ? "空だニャ" : resultText;
    var payload = new
    {
        channel = "#azurefunctions",
        username = "C# Evaluator",
        text = message,
        icon_url = "https://azure.microsoft.com/svghandler/visual-studio-team-services/?width=300&height=300",
    };
    
    var jsonString = JsonConvert.SerializeObject(payload);
    using (var client = new HttpClient())
    {
        var res = await client.PostAsync(_slackWebhookUrl, new StringContent(jsonString, Encoding.UTF8, "application/json"));
        return req.CreateResponse(res.StatusCode, new {
            body = $"CSharp Evaluate message. Message : {message}",
        });
    }
}