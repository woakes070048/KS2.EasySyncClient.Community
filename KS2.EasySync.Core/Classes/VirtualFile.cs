/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;

namespace KS2.EasySync.Core
{
    [Serializable]
    public class VirtualFile : VirtualElement
    {
        public string FileTemporaryCopyFullPath;
        public string SeedTemporaryCopyFullPath;
        public string FileHash;
        public string TemporaryDownloadFilePathFull;

        /// <summary>
        /// Quand rechargement depuis la base de données
        /// </summary>
        /// <param name="pFileId"></param>
        /// <param name="pRelativePath"></param>
        /// <param name="pRemoteID"></param>
        /// <param name="pFileHash"></param>
        /// <param name="pCustomProperties"></param>
        public VirtualFile(Guid pFileId, string pRelativePath, String pRemoteID, string pFileHash, string pCustomProperties)
            : base(VirtualElementType.File, pFileId, pRelativePath)
        {
            this.RemoteID = pRemoteID;
            this.FileHash = pFileHash;
            this.CustomProperties = pCustomProperties;
        }

        /// <summary>
        /// A la création d'un nouveau fichier
        /// </summary>
        /// <param name="pRelativePath"></param>
        public VirtualFile(string pRelativePath)
            : base(VirtualElementType.File, pRelativePath)
        {
        }

        /// <summary>
        /// Save virtual file data to database
        /// </summary>
        /// <param name="RepositoryId"></param>
        public void Serialize(Int32 RepositoryId)
        {
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return;

            object QueryResult = null;
            oSQLHelper.SetCommandText("SELECT COUNT(*) FROM VFile WHERE Id_File = @FileId");
            oSQLHelper.SetCommandParameter("@FileId", DbType.String, 36, this.ElementId.ToString());
            if (!oSQLHelper.ExecuteScalar(ref QueryResult)) return;

            if (Convert.ToInt16(QueryResult) == 0)
            {
                oSQLHelper.SetCommandText(@" INSERT INTO VFile(Id_File, Fk_Repository, Fk_ParentFolder, Name, RelativePath, RemoteId, Hash, CustomInfo)" +
                                          @" VALUES(@FileId,@RepositoryId,@ParentFolderId, @Name,@RelativePath,@RemoteId, @Hash,@CustomInfo)");
            }
            else
            {
                oSQLHelper.SetCommandText(@" UPDATE VFile" +
                                          @" SET Name = @Name" +
                                          @" ,Fk_ParentFolder = @ParentFolderId" +
                                          @" ,RelativePath = @RelativePath" +
                                          @" ,RemoteId = @RemoteId" +
                                          @" ,CustomInfo = @CustomInfo" +
                                          @" ,Hash = @Hash" +
                                          @" WHERE Id_File = @FileId");
            }

            oSQLHelper.SetCommandParameter("@FileId", DbType.String, 36, this.ElementId.ToString());
            oSQLHelper.SetCommandParameter("@RepositoryId", DbType.Int32, null, RepositoryId);
            oSQLHelper.SetCommandParameter("@ParentFolderId", DbType.String, 36, this.ParentElement.ElementId.ToString());
            oSQLHelper.SetCommandParameter("@Name", DbType.String, 200, this.CurrentName);
            oSQLHelper.SetCommandParameter("@RelativePath", DbType.String, 200, this.PathRelative);
            oSQLHelper.SetCommandParameter("@RemoteId", DbType.String, 200, this.RemoteID);
            oSQLHelper.SetCommandParameter("@Hash", DbType.String, 200, this.FileHash == null ? "" : this.FileHash);
            oSQLHelper.SetCommandParameter("@CustomInfo", DbType.String, 200, this.CustomProperties);
            oSQLHelper.ExecuteNonQuery();

            oSQLHelper.Dispose();
        }
    }
}