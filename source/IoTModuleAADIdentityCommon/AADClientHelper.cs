using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IoTModuleAADIdentityCommon
{
    public class AADClientHelper
    {
        const string WorkloadApiVersion = "2019-01-30";
        const string WorkloadUriVariableName = "IOTEDGE_WORKLOADURI";
        const string IotHubHostnameVariableName = "IOTEDGE_IOTHUBHOSTNAME";
        const string GatewayHostnameVariableName = "IOTEDGE_GATEWAYHOSTNAME";

        const string DeviceIdVariableName = "IOTEDGE_DEVICEID";
        const string ModuleIdVariableName = "IOTEDGE_MODULEID";

        const string ModuleGenerationIdVariableName = "IOTEDGE_MODULEGENERATIONID";


        private string _edgeDeviceId;
        private string _edgeModuleId;


        private string _iothubHostName;
        private string _gatewayHostName;


        private string _userName;
        private string _password;


        private ILogger _logger;
        private ModuleClient _moduleClient;

        public AADModuleTwin ModuleTwin { get; set; }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logger"></param>
        public AADClientHelper(ModuleClient moduleClient, ILogger logger)
        {
            this._moduleClient = moduleClient;
            this._logger = logger;

            _iothubHostName = Environment.GetEnvironmentVariable(IotHubHostnameVariableName);
            _gatewayHostName = Environment.GetEnvironmentVariable(GatewayHostnameVariableName);

            _edgeDeviceId = Environment.GetEnvironmentVariable(DeviceIdVariableName);
            _edgeModuleId = Environment.GetEnvironmentVariable(ModuleIdVariableName);
        }



        /// <summary>
        /// Send a Telemetry message with the Module Client to IoT Hub to create the AAD Identity
        /// </summary>
        /// <param name="moduleClient"></param>
        /// <param name="operationType"></param>
        /// <returns></returns>
        public async Task RequestAADOperation(OperationTypeEnum operationType)
        {
            //Just ask for the creation of the module identity
            string payloadJsonString = JsonConvert.SerializeObject(new { OperationType = operationType });

            _logger.LogInformation($"Sending Telemetry message to request a new identity:\n{payloadJsonString}");

            byte[] payload = Encoding.UTF8.GetBytes(payloadJsonString);

            var message = new Message(payload);
            message.ContentType = "application/json";
            message.ContentEncoding = "utf-8";

            //ad an application specific property to mark this telemetry message as an AAD Module Identity Operation
            //this will be used for the filtering / routing of the message to the appriopriate component on the cloud
            //to handle such kind of request
            message.Properties.Add(AADModuleIdentityOperationMessage.TelemetryTypePropertyName,
                AADModuleIdentityOperationMessage.TelemetryTypeValue);

            await _moduleClient.SendEventAsync("AADModuleIdentityOpOutput", message);
        }


        /// <summary>
        /// Process the Twin update looking for AAD updated info
        /// </summary>
        /// <param name="twinCollection"></param>
        /// <param name="desiredProperties"></param>
        public void ProcessTwinUpdate(TwinCollection twinCollection)
        {
            //parse the module twin!
            ModuleTwin = AADModuleTwin.Parse(twinCollection.ToJson());
        }

        public void ProcessTwinUpdate(Twin twins)
        {
            //parse the module twin!
            ModuleTwin = AADModuleTwin.Parse(twins.ToJson());
        }


        /// <summary>
        /// This method should return the AAD token reppresenting the module identity!
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetAADToken(CancellationToken stopToken)
        {
            if (!ModuleTwin.CheckAADOperationStatus(OperationStatusEnum.IdentityCreated))
            {
                //ok start the telemetry process

                await this.RequestAADOperation(OperationTypeEnum.CreateIdentity);

                // Instantiate the CancellationTokenSource.
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15.0));

                //wait 15 seconds 
                while ((!cts.IsCancellationRequested) && (!stopToken.IsCancellationRequested))
                {
                    if (!ModuleTwin.CheckAADOperationStatus(OperationStatusEnum.IdentityCreated))
                    { 
                        await Task.Delay(1000);
                    }   
                }
            }

            if (!ModuleTwin.CheckAADOperationStatus(OperationStatusEnum.IdentityCreated))
            {
                throw new ApplicationException("AAD Token is not yet ready for this module, please try again later.");
            }


            if (string.IsNullOrEmpty(_userName))
            {
                _userName = BuildUserName(_edgeDeviceId, _edgeModuleId);
            }

            if (string.IsNullOrEmpty(_password))
            {
                _password = await SignStringWithModuleKeyAsync(_userName);
            }


            //CALL THE AAD TOKEN ENDPOINT to collect the token!!!



            return null;
        }



        /// <summary>
        /// Sign the given payload using the Worload API of the locasl Security Deamon.async
        /// The signed payload is returned as Base64 string.
        /// </summary>
        private async Task<string> SignStringWithModuleKeyAsync(string payload)
        {
            string generationId = Environment.GetEnvironmentVariable(ModuleGenerationIdVariableName);
            Uri workloadUri = new Uri(Environment.GetEnvironmentVariable(WorkloadUriVariableName));

            string signedPayload = string.Empty;
            using (HttpClient httpClient = Microsoft.Azure.Devices.Edge.Util.HttpClientHelper.GetHttpClient(workloadUri))
            {
                httpClient.BaseAddress = new Uri(Microsoft.Azure.Devices.Edge.Util.HttpClientHelper.GetBaseUrl(workloadUri));

                var workloadClient = new WorkloadClient(httpClient);

                var signRequest = new SignRequest()
                {
                    KeyId = "primary", // or "secondary"
                    Algo = SignRequestAlgo.HMACSHA256,
                    Data = Encoding.UTF8.GetBytes(payload)
                };

                var signResponse = await workloadClient.SignAsync(WorkloadApiVersion, _edgeModuleId, generationId, signRequest);
                signedPayload = Convert.ToBase64String(signResponse.Digest);
            }

            return signedPayload;
        }



        public static string BuildUserName(string deviceId, string moduleId)
        {
            return $"iot_{deviceId}_{moduleId}";
        }
    }
}
