// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Devices;
using System.Threading.Tasks;
using IoTModuleAADIdentityCommon;

namespace IoTModuleAADIdentityManagement
{
    public static class AADModuleIdentityFunctions
    {
        /// <summary>
        /// This function will be triggered by a telemetry message coming from a module
        /// reqesting for the necessay info of the AAD Identity for the module.
        /// </summary>
        /// <param name="eventGridEvent"></param>
        /// <param name="log"></param>
        [FunctionName("ModuleIdentityRequest")]
        public static async Task ModuleIdentityRequest(
            [EventGridTrigger]EventGridEvent eventGridEvent, 
            ILogger log)
        {
            log.LogInformation("Start handling ModuleIdentityRequest");

            var iotHubConnectionString = System.Environment.GetEnvironmentVariable("iotHubConnectionString", EnvironmentVariableTarget.Process);

            RegistryManager registryManager = null;
          
            if (string.IsNullOrEmpty(iotHubConnectionString))
            {
                log.LogWarning($"Missing IoT Hub Connection String!");

                return;
            }

            if (eventGridEvent.EventType != "Microsoft.Devices.DeviceTelemetry")
            {
                log.LogWarning($"Wrong Event Grid EventType closing. {eventGridEvent.EventType}");

                return;
            }


            //Extract Telemetry Payload 
            //which operaton??   Create / Renew identiy?

            var eventDataString = eventGridEvent.Data.ToString();

            log.LogDebug($"Deserializing event data {eventDataString}");


            //Note: we assume a JSON payload in the body, 'UTF-8' Encoded AND 'application/json' content type. Otherwise body will be base64 encoded
            var deviceEvent = JsonConvert.DeserializeObject<AADModuleIdentityOperationMessage>(eventDataString);


            if (deviceEvent.Properties != null)
            {
                if (deviceEvent.Properties.TelemetryType != null)
                {
                    if (deviceEvent.Properties.TelemetryType == "AADModuleIdentityOperation")
                    {
                        //ok this is an AAD Module Identity Operation telemetry message

                        var deviceId = deviceEvent.SystemProperties.EdgeDeviceId;
                        var moduleId = deviceEvent.SystemProperties.EdgeModuleId;

                        //Get Module info from IoT Hub 
                        registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);

                        var module = await registryManager.GetModuleAsync(deviceId as string, moduleId as string);

                        if (module == null)
                        {
                            log.LogError("Error in event grid processing, module info could not be loaded. Exiting function.");
                            return;
                        }


                        //download the Module Twin
                        var deviceTwin = await registryManager.GetTwinAsync(deviceId, moduleId);

                        //and parse into the object model
                        var updateTwin = AADModuleTwin.Parse(deviceTwin.ToJson());


                        //get the opeation type from the body of the telemetry message
                        var opType = deviceEvent.Body.OperationType;


                        //Updating the Twin to indicate that the telemetry message was received and pending to be processes
                        OperationStatusEnum updatedStatus = OperationStatusEnum.Invalid;

                        if (opType == OperationTypeEnum.CreateIdentity)
                        {
                            updatedStatus = OperationStatusEnum.CreatingIdentity;
                        }

                        if (opType == OperationTypeEnum.RefreshIdentity)
                        {
                            updatedStatus = OperationStatusEnum.RefreshingIdentity;
                        }

                       

                        updateTwin.Properties.Desired.AADIdentityStatus = updatedStatus;

                        string newTwin = updateTwin.ToJson();

                        log.LogDebug($"Updating Module twin\n{newTwin}");

                        var updateResult = await registryManager.UpdateTwinAsync(deviceId, moduleId, newTwin, deviceTwin.ETag);

                        log.LogInformation($"Module Twin updated to version: {updateResult.Version}");








                        //Generate the User Name and Password (from Module Identity information)
                        //This process is documented as "STEP 5 / 6"

                        var userName = AADClientHelper.BuildUserName(deviceId, moduleId);

                        var moduleKey = Convert.FromBase64String(module.Authentication.SymmetricKey.PrimaryKey);

                        var password = SecretHelper.ComputeSecretWithHMACSHA256(moduleKey, userName);


                        //Create or Update the Identity in AAD




                        ///TODO TODO TODO




                        //Update Module Twin to reflect the operation feedback

                        ///TODO REMOVE THE PASSWORD FROM THE TAG THIS IS JUST FOR TEST 

                        updatedStatus = OperationStatusEnum.IdentityCreated;

                        newTwin =
                        @"{
                            tags: {
                                aadIdentityPassword: '[Password]'
                            },
                            properties: {
                                desired: {
                                    aadIdentityStatus: '[Status]',
                                    aadIdentityUserName: '[UserName]'
                                }
                            }
                        }".Replace("[UserName]",userName).Replace("[Password]",password).Replace("[Status]", updatedStatus.ToString());


                        updateTwin.Tags.AADIdentityPassword = password;
                        updateTwin.Properties.Desired.AADIdentityStatus = updatedStatus;
                        updateTwin.Properties.Desired.AADIdentityUserName = userName;

                        newTwin = updateTwin.ToJson();

                        log.LogDebug($"Updating Module twin\n{newTwin}");

                        try
                        {
                            //reag again the twin to obtain the latest etag
                            deviceTwin = await registryManager.GetTwinAsync(deviceId, moduleId);

                            //Update Device Twin to notify that the user in the AAD is already created!
                            updateResult = await registryManager.UpdateTwinAsync(deviceId, moduleId, newTwin, deviceTwin.ETag);
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, $"Error while updating Module Twin");
                        }

                        log.LogInformation($"Module Twin updated to version: {updateResult.Version}");
                    }
                }
            }
        }



        
    }
}
