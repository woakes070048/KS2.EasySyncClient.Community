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
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NLog;

namespace KS2.EasySync.Core
{
    public class Tools
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static bool EnableCompression(string FileName)
        {
            return !Enumerable.Contains(new String[] { ".001", ".7Z", ".ARJ", ".BZIP", ".BZIP2", ".CAB", ".DEB", ".EAR", ".GZ", ".HQX", ".JAR", ".RAR", ".RPM", ".SEA", ".SIT", ".TAR", ".WAR", ".ZIP" }, Path.GetExtension(FileName).ToUpper());
        }

        public static byte[] CompressByteArray(byte[] buffer, int size)
        {
            System.IO.MemoryStream InputStream = new System.IO.MemoryStream();
            GZipStream ZipStream = new GZipStream(InputStream, CompressionMode.Compress);

            //Compress (ie write the buffer through the ZipStream)
            ZipStream.Write(buffer, 0, size);

            //Close, DO NOT FLUSH cause bytes will go missing...
            ZipStream.Close();

            //Transform byte[] zip data to string
            byte[] ZippedBuffer = InputStream.ToArray();

            InputStream.Close();
            ZipStream.Dispose();
            InputStream.Dispose();

            return ZippedBuffer;
        }

        public static byte[] DeCompressByteArray(byte[] buffer)
        {
            //Prepare for compress
            System.IO.MemoryStream InputStream = new System.IO.MemoryStream(buffer);
            System.IO.MemoryStream OutputStream = new System.IO.MemoryStream();
            GZipStream ZipStream = new GZipStream(InputStream, CompressionMode.Decompress);

            byte[] bytes = new byte[4096];
            int n;
            while ((n = ZipStream.Read(bytes, 0, bytes.Length)) != 0)       // While zip results output bytes from input stream
            {
                OutputStream.Write(bytes, 0, n);                            // Write the unzipped bytes into output stream
            }

            //Close, DO NOT FLUSH cause bytes will go missing...
            ZipStream.Close();

            //Transform byte[] zip data to string
            byte[] UnZippedBuffer = OutputStream.ToArray();

            InputStream.Close();
            OutputStream.Close();
            ZipStream.Dispose();
            InputStream.Dispose();
            OutputStream.Dispose();

            return UnZippedBuffer;
        }

        public static bool GetFileHash(string FileFullPath, out string FileHash)
        {
            string sHash = "";
            System.IO.FileStream sr = null;

            try
            {
                sr = File.OpenRead(FileFullPath);
                MD5CryptoServiceProvider md5h = new MD5CryptoServiceProvider();
                sHash = BitConverter.ToString(md5h.ComputeHash(sr));
            }
            catch
            {
                return false;
            }
            finally
            {
                FileHash = sHash;
                if (sr != null) sr.Close();
            }

            return true;
        }

        public static bool GetFileHash(System.IO.FileStream FS, out string FileHash)
        {
            string sHash = "";

            MD5CryptoServiceProvider md5h = new MD5CryptoServiceProvider();
            sHash = BitConverter.ToString(md5h.ComputeHash(FS));

            FileHash = sHash;

            return true;
        }

        /// <summary>
        /// Get a stream to a file
        /// </summary>
        /// <param name="FilePath"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static bool IsFileAvailable(string FilePath, ref System.IO.FileStream stream)
        {
            try
            {
                FileInfo fInfo = new FileInfo(FilePath);
                stream = fInfo.Open(System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Load proxy configuration from database
        /// </summary>
        public static void LoadConfig()
        {
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return;

            object QueryResult = null;
            oSQLHelper.SetCommandText("SELECT ParamValue FROM Param WHERE ParamName = 'PROXY_MODE'");
            if (!oSQLHelper.ExecuteScalar(ref QueryResult)) return;

            if (QueryResult == null)
            {
                Globals.GlbProxyMode = 0; //Default is : No proxy
            }
            else
            {
                Globals.GlbProxyMode = Convert.ToInt16(QueryResult);

                if (Globals.GlbProxyMode == 2) //If a proxy is specified
                {
                    oSQLHelper.SetCommandText("SELECT ParamValue FROM Param WHERE ParamName = 'PROXY_URL'");
                    oSQLHelper.ExecuteScalar(ref QueryResult);
                    Globals.GlbProxyURL = QueryResult.ToString();

                    oSQLHelper.SetCommandText("SELECT ParamValue FROM Param WHERE ParamName = 'PROXY_AUTH'");
                    oSQLHelper.ExecuteScalar(ref QueryResult);
                    if (QueryResult == null) Globals.GlbProxyAuthentication = false;
                    else Globals.GlbProxyAuthentication = Convert.ToBoolean(Convert.ToInt16(QueryResult));

                    if (Globals.GlbProxyAuthentication)
                    {
                        oSQLHelper.SetCommandText("SELECT ParamValue FROM Param WHERE ParamName = 'PROXY_LOGIN'");
                        oSQLHelper.ExecuteScalar(ref QueryResult);
                        if (QueryResult != null) Globals.GlbProxyLogin = QueryResult.ToString();

                        oSQLHelper.SetCommandText("SELECT ParamValue FROM Param WHERE ParamName = 'PROXY_PASSWORD'");
                        oSQLHelper.ExecuteScalar(ref QueryResult);
                        if (QueryResult != null)
                        {
                            String EncryptedPassword = QueryResult.ToString();
                            try
                            {
                                Globals.GlbProxyPassword = Tools.Decrypt(EncryptedPassword);
                            }
                            catch
                            {
                                Globals.GlbProxyPassword = "";
                                logger.Debug("Stored proxy password is invalid. Using a empty one");
                            }
                        }
                    }
                }
            }

            oSQLHelper.Dispose();
        }

        /// <summary>
        /// Encrypts a given password and returns the encrypted data
        /// as a base64 string.
        /// </summary>
        /// <param name="plainText">An unencrypted string that needs
        /// to be secured.</param>
        /// <returns>A base64 encoded string that represents the encrypted
        /// binary data.
        /// </returns>
        /// <remarks>This solution is not really secure as we are
        /// keeping strings in memory. If runtime protection is essential,
        /// <see cref="SecureString"/> should be used.</remarks>
        /// <exception cref="ArgumentNullException">If <paramref name="plainText"/>
        /// is a null reference.</exception>
        public static string Encrypt(string value)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;

            byte[] buffer = ProtectedData.Protect(Encoding.Unicode.GetBytes(value), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(buffer);
        }

        /// <summary>
        /// <summary>
        /// Decrypts a given string.
        /// </summary>
        /// <param name="cipher">A base64 encoded string that was created
        /// through the <see cref="Encrypt(string)"/> or
        /// <see cref="Encrypt(SecureString)"/> extension methods.</param>
        /// <returns>The decrypted string.</returns>
        /// <remarks>Keep in mind that the decrypted string remains in memory
        /// and makes your application vulnerable per se. If runtime protection
        /// is essential, <see cref="SecureString"/> should be used.</remarks>
        /// <exception cref="ArgumentNullException">If <paramref name="cipher"/>
        /// is a null reference.</exception>
        public static string Decrypt(string value)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;

            byte[] buffer = Convert.FromBase64String(value);
            return Encoding.Unicode.GetString(ProtectedData.Unprotect(buffer, null, DataProtectionScope.CurrentUser));
        }

        /// <summary>
        /// Retrieve parent path from Element path
        /// </summary>
        /// <param name="ElementPath"></param>
        /// <returns></returns>
        public static string GetParentPath(String ElementPath)
        {
            string ParentPath;

            if (ElementPath.IndexOf(Path.DirectorySeparatorChar) == -1)
            {
                return ""; //Parent path of root is ""
            }
            else
            {
                try
                {
                    ParentPath = ElementPath.Substring(0, ElementPath.LastIndexOf(Path.DirectorySeparatorChar));
                }
                catch (Exception ex)
                {
                    logger.DebugException(String.Format("In GetParentPath - Invalid Path : [{0}]", ElementPath), ex);
                    ParentPath = "";
                }
            }

            return ParentPath;
        }

        /// <summary>
        /// Retrieve the folder name from Element path
        /// </summary>
        /// <param name="ElementPath"></param>
        /// <returns></returns>
        public static string GetFolderNameFromPath(String ElementPath)
        {
            string ParentPath;
            try
            {
                ParentPath = ElementPath.Substring(ElementPath.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            }
            catch (Exception ex)
            {
                logger.Debug("In GetFolderNameFromPath - " + ex.Message);
                ParentPath = "";
            }
            return ParentPath;
        }
    }
}