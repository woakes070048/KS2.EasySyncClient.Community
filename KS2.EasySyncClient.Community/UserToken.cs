/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;

namespace KS2.EasySyncClient
{
    public class UserToken
    {
        public String UserLogin { get; set; }
        public String MachineName { get; set; }
        public String ProductCode { get; set; }
        public String ProductVersion { get; set; }
        public String SystemVersion { get; set; }
    }
}
