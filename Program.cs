using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
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

public Func<System.Net.Http.HttpRequestMessage,System.Security.Cryptography.X509Certificates.X509Certificate2,System.Security.Cryptography.X509Certificates.X509Chain,System.Net.Security.SslPolicyErrors,bool> ServerCertificateCustomValidationCallback { get; set; }
    static void VerifyConnectivityToTokenApi(IConfigurationSection config)
    {
      Log.Debug("Testing Server Connectivity via the token api endpoint.");

      var handler = new SocketsHttpHandler();
      handler.SslOptions.RemoteCertificateValidationCallback = (sender, remoteCertificate, chain, policyErrors) => {
        foreach (var element in chain.ChainElements)
        {
          Log.Debug ("Element name: {0}", element.Certificate.SubjectName.Name);
          Log.Debug ("Element issuer name: {0}", element.Certificate.Issuer);
          Log.Debug ("Element certificate valid until: {0}", element.Certificate.NotAfter);
          Log.Debug ("Element certificate is valid: {0}", element.Certificate.Verify ());
          Log.Debug ("Element error status length: {0}", element.ChainElementStatus.Length);
          Log.Debug ("Element information: {0}", element.Information);
          Log.Debug ("Number of element extensions: {0}", element.Certificate.Extensions.Count);
          Log.Debug ("Number of ChainElementStatuses: {0}", element.ChainElementStatus.Length);
          if (element.ChainElementStatus.Length > 0)
          {
            for (int index = 0; index < element.ChainElementStatus.Length; index++)
            {
              Log.Debug ("{0}: {1}", element.ChainElementStatus[index].Status, element.ChainElementStatus[index].StatusInformation);
            }
          }
          Console.WriteLine(Environment.NewLine);
        }
        if (policyErrors == SslPolicyErrors.None)
        {
          Log.Debug("No TLS Policy Errors.");
          return true;
        }

        if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
          Log.Debug("TLS Policy Chain Errors");

          foreach(var status in chain.ChainStatus)
          {
            Log.Debug($"Chain Status: {status.StatusInformation}");
          }
        }

        if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
          Log.Debug("TLS Remote Certificate Name Mismatch");
        }

        if (policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
          Log.Debug("TLS Remote Certificate Not Available");
        }

        return false;
      };
      
      var httpClient = new HttpClient(handler);
      
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
        Log.Debug($"{item.Slug}: {item.ItemValue}");
      }
    }
  }
}
