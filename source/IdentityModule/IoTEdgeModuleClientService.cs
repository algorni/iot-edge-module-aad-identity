using IoTModuleAADIdentityCommon;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace IdentityModule
{
    public class IoTEdgeModuleClientService : BackgroundService
    {
        private readonly ILogger _logger;

        public ModuleClient EdgeHubModuleClient { get; private set; } = null;

        private AADClientHelper _aadClientHelper;

        public IoTEdgeModuleClientService(ILogger<IoTEdgeModuleClientService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
            _logger.LogInformation("Starting IoT Edge Module Client");

            try
            {
                AmqpTransportSettings amqpSetting = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
                ITransportSettings[] settings = { amqpSetting };

                // Open a connection to the Edge runtime
                EdgeHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                await EdgeHubModuleClient.OpenAsync();

                //create the AAD Client Helper class
                _aadClientHelper = new AADClientHelper(EdgeHubModuleClient, _logger);

                _logger.LogInformation("IoT Hub module client initialized.");


                _logger.LogInformation("Registering Desired Property Update callback.");

                await EdgeHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback, null);

                //and perform at load time a get twin
                var twins = await EdgeHubModuleClient.GetTwinAsync(stopToken);

                //handle Twin Updates with the aad client helper
                _aadClientHelper.ProcessTwinUpdate(twins);

                string token = null;

                while (!stopToken.IsCancellationRequested)
                {
                    //simple test ...  GET an AAD TOKEN
                    if (token == null)
                    {
                        token = await _aadClientHelper.GetAADToken(stopToken);
                    }

                    await Task.Delay(10000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while initializing IoT Edge Module Client");

                await Program.Host.StopAsync();
            }

            _logger.LogInformation("Stopping IoT Edge Module Client");
        }



        private Task desiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            //handle Twin Updates with the aad client helper
            _aadClientHelper.ProcessTwinUpdate(desiredProperties);

            return Task.CompletedTask;
        }




    }
}
