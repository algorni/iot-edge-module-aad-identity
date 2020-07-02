namespace IdentityModule
{
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    class Program
    {
        public static IHost Host = null;

        static void Main(string[] args)
        {
            var hostBuilder = new HostBuilder()
                       .ConfigureServices(services =>
                       {
                           services.AddLogging();
                           services.AddHostedService<IoTEdgeModuleClientService>();
                       }
                     );

            hostBuilder.ConfigureLogging(
                loggingOptions => loggingOptions.AddConsole(opt => opt.TimestampFormat = "[HH:mm:ss] ").AddDebug());

            hostBuilder.UseConsoleLifetime(o => o.SuppressStatusMessages = true);

            Host = hostBuilder.Build();

            ILogger logger = Host.Services.GetService<ILogger<Program>>();

            logger.LogInformation("Starting Module");

            try
            {
                Host.Run();
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Exiting...");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while executing the module, see stack trace...");
            }
        }
    }
}
