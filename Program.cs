using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;
using Thycotic.SecretServer.Sdk.Extensions.Integration.Clients;
using Thycotic.SecretServer.Sdk.Extensions.Integration.Models;
using Thycotic.SecretServer.Sdk.Infrastructure.Models;

namespace thycotic_sdk_issue
{
  public class SecretRecords
  {
    public int Id { get; set; }
  }

  public class PagingOfSecretSummary
  {
    public List<SecretRecords> Records { get; set; }
  }

  class Program
  {
    static void Main(string[] args)
    {
      var builder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();
      
      var configuration = builder.Build();

      Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .Enrich.FromLogContext()
        .MinimumLevel.Debug()
        .CreateLogger();
      
      Log.Debug("Thycotic Secret Server Initialization");

      var env = System.Environment.GetEnvironmentVariables();

      Log.Debug("Loading Thycotic Environment Variables");
      foreach(DictionaryEntry e in env){
        if (((string)e.Key).StartsWith("Thycotic__"))
        {
          Log.Debug($"{e.Key}={e.Value}");
        }
      }
      var config = configuration.GetSection("Thycotic");
      var url = config.GetValue<string>("Uri");
      var ruleName = config.GetValue<string>("RuleName");
      var ruleKey = config.GetValue<string>("RuleKey");
      var cacheAge = config.GetValue<int>("CacheAge");
      var templateName = config.GetValue<string>("SecretTemplateName");
      var searchText = config.GetValue<string>("SearchText");

      var resetToken = Path.GetRandomFileName().Replace(".", string.Empty);

      var configLog = new {
        Uri = url,
        RuleName = ruleName,
        RuleKey = ruleKey,
        CacheAge = cacheAge,
        SecretTemplateName = templateName,
        SearchText = searchText,
        ResetToken = resetToken
      };

      Log.Debug($"Loaded Configuration: \n{JsonConvert.SerializeObject(configLog, Formatting.Indented)}");

      var client = new SecretServerClient();

      Log.Debug("Client Constructed. Configuring Thycotic SecretServerClient");

      client.Configure(new ConfigSettings
      {
        SecretServerUrl = url,
        RuleName = ruleName,
        RuleKey = ruleKey,
        CacheStrategy = CacheStrategy.CacheThenServerAllowExpired,
        CacheAge = cacheAge,
        ResetToken = resetToken
      });

      Log.Debug("Thycotic Secret Server Client Initialized");

      var uriBuilder = new UriBuilder(url)
      {
        Path = "/api/v1/secrets",
        Query = $"secretTemplateName={templateName}&filter.searchText={searchText}"
      };

      Log.Debug($"Secret Server Search Uri: {uriBuilder.Uri.AbsoluteUri}");

      // Would be really nice if the SDK had a function to do this:
      var requestMessage = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri.AbsoluteUri);
      requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", client.GetAccessToken());
      var httpClient = new HttpClient();
      var result = httpClient.SendAsync(requestMessage).Result;
      var secretResponse = result.Content.ReadAsStringAsync().Result;
      var secretRecords = JsonConvert.DeserializeObject<PagingOfSecretSummary>(secretResponse);
      var secretId = secretRecords.Records[0].Id;

      Log.Debug($"Retrieving Secret Id '{secretId}' from Secret Server.");

      var secret = client.GetSecret(secretId);

      foreach(var item in secret.Items)
      {
        Console.WriteLine($"{item.Slug}: {item.ItemValue}");
      }
    }
  }
}
