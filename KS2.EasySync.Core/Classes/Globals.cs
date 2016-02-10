/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
#if __MonoCS__
	using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Text;
using System.Security.Cryptography;
using NLog;
using System.Data;

namespace KS2.EasySync.Core
{
    public static class Globals
    {
        public static string GlbFileName = "EasySync.db";
        public static string GlbDBFilePath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySync"), GlbFileName);
        public static string GlbConnectionString = String.Format(@"Data Source={0};Version=3", GlbDBFilePath);

        public static Int16 GlbProxyMode;
        public static string GlbProxyURL;
        public static bool GlbProxyAuthentication;
        public static string GlbProxyLogin;
        public static string GlbProxyPassword;
        public static string GlbAppFolder;
    }
}