using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace IoTModuleAADIdentityCommon
{
    public class SecretHelper
    {
        /// <summary>
        /// Generate the Password 
        /// </summary>
        /// <param name="moduleKey">Symmetric key enrollment group primary/secondary key value</param>
        /// <param name="commonSecret">the calculated user name </param>
        /// <returns>the password for the AAD identity</returns>
        public static string ComputeSecretWithHMACSHA256(byte[] moduleKey, string calculatedUserName)
        {
            using (var hmac = new HMACSHA256(moduleKey))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(calculatedUserName)));
            }
        }
    }
}
