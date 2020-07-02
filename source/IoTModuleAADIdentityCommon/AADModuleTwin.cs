using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace IoTModuleAADIdentityCommon
{
    public class AADModuleTwin
    {
        [JsonProperty("tags")]
        public Tags Tags { get; set; }


        [JsonProperty("properties")]
        public PropertyContainer Properties { get; set; }

        public static AADModuleTwin Parse(string json)
        {
            return JsonConvert.DeserializeObject<AADModuleTwin>(json);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }


        public bool CheckAADOperationStatus(OperationStatusEnum status)
        {
            if (this.Properties != null &&
                this.Properties.Desired != null &&
                this.Properties.Desired.AADIdentityStatus == status)
            {
                return true;
            }

            return false;
        }
    }


    public class Tags
    {
        [JsonProperty("aadIdentityPassword")]
        public string AADIdentityPassword { get; set; }
    }

    public class PropertyContainer
    {
        [JsonProperty("desired")]
        public DesiredProperties Desired { get; set; }

        //[JsonProperty("reported")]
        //public Properties Reported { get; set; }
    }

    public class DesiredProperties
    {
        [JsonProperty("aadIdentityStatus")]
        [JsonConverter(typeof(StringEnumConverter))]
        public OperationStatusEnum AADIdentityStatus { get; set; }

        [JsonProperty("aadIdentityUserName")]
        public string AADIdentityUserName { get; set; }
    }

    public enum OperationStatusEnum
    {
        CreatingIdentity,
        RefreshingIdentity,
        IdentityCreated,
        Invalid
    }
}
