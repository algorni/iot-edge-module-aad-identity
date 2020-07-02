using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace IoTModuleAADIdentityCommon
{
    public class AADModuleIdentityOperationMessage
    {
        public static string TelemetryTypePropertyName = "TelemetryType";

        public static string TelemetryTypeValue = "AADModuleIdentityOperation";


        [JsonProperty("properties")]
        public DataProperties Properties { get; set; }

        [JsonProperty("systemProperties")]
        public DataSystemProperties SystemProperties { get; set; }

        [JsonProperty("body")]
        public DataBody Body { get; set; }
    }

    public class DataProperties
    {
        [JsonProperty("TelemetryType")]
        public string TelemetryType { get; set; }
    }

    public class DataSystemProperties
    {
        [JsonProperty("iothub-connection-device-id")]
        // Edge device id where an Identity Translation Module (ITM) runs
        public string EdgeDeviceId { get; set; }

        [JsonProperty("iothub-connection-module-id")]
        public string EdgeModuleId { get; set; } // ITM module id
    }

    public class DataBody
    {
        [JsonProperty("OperationType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public OperationTypeEnum OperationType { get; set; }
    }

    public enum OperationTypeEnum
    {
        CreateIdentity,
        RefreshIdentity
    }
}
