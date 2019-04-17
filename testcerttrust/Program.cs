using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace testcerttrust
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

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
            var url = configuration.GetSection("TestApp")["Uri"];
            Log.Debug("Uri: {0}", url);
            var handler = new SocketsHttpHandler();
            handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => ServerCertificateCustomValidationCallback(sender, certificate, chain, errors);

            var httpClient = new HttpClient(handler);

            var result = httpClient.GetAsync(url).Result;

            Log.Debug("Status Code: {0}", result.StatusCode);
            Log.Debug("Content: {0}", result.Content.ReadAsStringAsync().Result);
        }

        static bool ServerCertificateCustomValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors policyErrors)
        {
            foreach (var element in chain.ChainElements)
            {
                Log.Debug("Element name: {0}", element.Certificate.SubjectName.Name);
                Log.Debug("Element issuer name: {0}", element.Certificate.Issuer);
                Log.Debug("Element certificate valid until: {0}", element.Certificate.NotAfter);
                Log.Debug("Element certificate is valid: {0}", element.Certificate.Verify());
                Log.Debug("Element error status length: {0}", element.ChainElementStatus.Length);
                Log.Debug("Element information: {0}", element.Information);
                Log.Debug("Number of element extensions: {0}", element.Certificate.Extensions.Count);
                Log.Debug("Number of ChainElementStatuses: {0}", element.ChainElementStatus.Length);
                if (element.ChainElementStatus.Length > 0)
                {
                    for (int index = 0; index < element.ChainElementStatus.Length; index++)
                    {
                        Log.Debug("{0}: {1}", element.ChainElementStatus[index].Status, element.ChainElementStatus[index].StatusInformation);
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

                foreach (var status in chain.ChainStatus)
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
        }
    }
}
