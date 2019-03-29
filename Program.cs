using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
      DisplayThycoticEnvironmentVariables();
      var httpClient = new HttpClient();
      var builder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();
      
      var configuration = builder.Build();

      Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .Enrich.FromLogContext()
        .MinimumLevel.Debug()
        .CreateLogger();
      
      Log.Debug("Loading Configuration from Config Section");
      var config = configuration.GetSection("Thycotic");

      VerifyConnectivityToTokenApi(config);
      DemonstrateSdkFunctionality(config);
    }

    static void DisplayThycoticEnvironmentVariables()
    {
      var env = System.Environment.GetEnvironmentVariables();

      Log.Debug("Loading Thycotic Environment Variables");
      foreach(DictionaryEntry e in env){
        if (((string)e.Key).StartsWith("Thycotic__"))
        {
          Log.Debug($"{e.Key}={e.Value}");
        }
      }
    }

    static void VerifyConnectivityToTokenApi(IConfigurationSection config)
    {
      Log.Debug("Testing Server Connectivity via the token api endpoint.");

      var httpClient = new HttpClient();
      var url = config.GetValue<string>("Uri");

      var tokenUriBuilder = new UriBuilder(url)
      {
        Path = "/oauth2/token",
      };

      var body = JsonConvert.SerializeObject(new {
        username = config.GetValue<string>("Username"),
        password = config.GetValue<string>("Password"),
        granttype = config.GetValue<string>("GrantType")
      }, Formatting.Indented);

      Log.Debug($"Submitting Post Request\nRequest Uri: {tokenUriBuilder.Uri.AbsoluteUri}\nBody: {body}");

      var message = new HttpRequestMessage(HttpMethod.Post, tokenUriBuilder.Uri.AbsoluteUri);
      message.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
      var tokenResponse = httpClient.SendAsync(message).Result;
      var tokenContent = tokenResponse.Content.ReadAsStringAsync().Result;

      Log.Debug($"StatusCode: {tokenResponse.StatusCode}\nToken Content: {tokenContent}");
    }

    static int SearchSecretId(SecretServerClient client, string uri, string template, string name)
    {
      Log.Debug("Using Rest API to search secrets.");

      var httpClient = new HttpClient();

      var uriBuilder = new UriBuilder(uri)
      {
        Path = "/api/v1/secrets",
        Query = $"secretTemplateName={template}&filter.searchText={name}"
      };

      Log.Debug($"Secret Server Search Uri: {uriBuilder.Uri.AbsoluteUri}" );

      // Would be really nice if the SDK had a function to do this:
      var requestMessage = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri.AbsoluteUri);
      requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", client.GetAccessToken());
      var result = httpClient.SendAsync(requestMessage).Result;
      var secretResponse = result.Content.ReadAsStringAsync().Result;
      var secretRecords = JsonConvert.DeserializeObject<PagingOfSecretSummary>(secretResponse);
      var secretId = secretRecords.Records[0].Id;

      Log.Debug($"Retrieving Secret Id '{secretId}' from Secret Server.");
      return secretId;
    }

    static void DemonstrateSdkFunctionality(IConfigurationSection config)
    {
      Log.Debug("Thycotic Secret Server Constrcutor");
      var client = new SecretServerClient();

      Log.Debug("Loading Configuration from Config Section");

      var sdkConfig = new {
        Uri = config.GetValue<string>("Uri"),
        RuleName = config.GetValue<string>("RuleName"),
        RuleKey = config.GetValue<string>("RuleKey"),
        CacheAge = config.GetValue<int>("CacheAge"),
        ResetToken = Path.GetRandomFileName().Replace(".", string.Empty)
      };

      Log.Debug($"Loaded Configuration: \n{JsonConvert.SerializeObject(sdkConfig, Formatting.Indented)}");

      Log.Debug("Configuring Thycotic SecretServerClient");

      client.Configure(new ConfigSettings
      {
        SecretServerUrl = sdkConfig.Uri,
        RuleName = sdkConfig.RuleName,
        RuleKey = sdkConfig.RuleKey,
        CacheStrategy = CacheStrategy.CacheThenServerAllowExpired,
        CacheAge = sdkConfig.CacheAge,
        ResetToken = sdkConfig.ResetToken
      });

      Log.Debug("Thycotic Secret Server Client Initialized");

      var templateName = config.GetValue<string>("SecretTemplateName");
      var searchText = config.GetValue<string>("SearchText");
      var secretId = SearchSecretId(client, sdkConfig.Uri, templateName, searchText);

      var secret = client.GetSecret(secretId);

      foreach(var item in secret.Items)
      {
        Console.WriteLine($"{item.Slug}: {item.ItemValue}");
      }
    }
  }
}
