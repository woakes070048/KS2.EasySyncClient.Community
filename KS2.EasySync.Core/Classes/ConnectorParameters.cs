/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KS2.EasySync.Core
{
    public class ConnectorParameter
    {
        public String Server { get; set; }
        public String Login { get; set; }
        public String Password { get; set; }
        public String SitePath { get; set; }
        public Int16 EnableFullScan { get; set; }

        /// <summary>
        /// Reload the connector parameters from database
        /// Need to detect the format of the parameters which have changed in times. Old format x|x|x|x| - New format Json
        /// </summary>
        /// <param name="DataAsString"></param>
        /// <returns></returns>
        public static ConnectorParameter Deserialize(String DataAsString)
        {
            String DecryptedParameters = Tools.Decrypt(DataAsString);

            ConnectorParameter CP;

            try
            {
                CP = Newtonsoft.Json.JsonConvert.DeserializeObject<ConnectorParameter>(DecryptedParameters);
            }
            catch
            {
                string[] parameters = DecryptedParameters.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                CP = new ConnectorParameter() { Server = parameters[0], Login = parameters[1], Password = parameters[2], SitePath = parameters[3], EnableFullScan = 0};
            }

            return CP;
        }

        public static String Serialize(ConnectorParameter CP)
        {
            return Tools.Encrypt(Newtonsoft.Json.JsonConvert.SerializeObject(CP));
        }
    }
}
