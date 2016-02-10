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

namespace KS2.EasySync.Core
{
    [Serializable]
    public class VirtualFolder : VirtualElement
    {
        /// <summary>
        /// Quand rechargement depuis la base de données
        /// </summary>
        /// <param name="pVirtualElementType"></param>
        /// <param name="pFolderId"></param>
        /// <param name="pRelativePath"></param>
        /// <param name="pRemoteID"></param>
        /// <param name="pCustomProperties"></param>
        public VirtualFolder(Guid pFolderId, string pRelativePath, String pRemoteID, string pCustomProperties)
            : base(VirtualElementType.Folder, pFolderId, pRelativePath)
        {
            this.RemoteID = pRemoteID;
            this.CustomProperties = pCustomProperties;
        }

        /// <summary>
        /// A la création d'un nouveau répertoire
        /// </summary>
        /// <param name="pVirtualElementType"></param>
        /// <param name="pRelativePath"></param>
        public VirtualFolder(string pRelativePath)
            : base(VirtualElementType.Folder, pRelativePath)
        {
        }

        public void Serialize(Int32 RepositoryId)
        {
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return;

            object QueryResult = null;
            oSQLHelper.SetCommandText("SELECT COUNT(*) FROM VFolder WHERE Id_Folder = @FolderId");
            oSQLHelper.SetCommandParameter("@FolderId", DbType.String, 36, this.ElementId.ToString());
            if (!oSQLHelper.ExecuteScalar(ref QueryResult)) return;

            if (Convert.ToInt16(QueryResult) == 0)
            {
                oSQLHelper.SetCommandText(@" INSERT INTO VFolder(Id_Folder, Fk_Repository, Name, RelativePath, RemoteId, CustomInfo)" +
                                          @" VALUES(@FolderId,@RepositoryId,@Name,@RelativePath,@RemoteId,@CustomInfo)");
            }
            else
            {
                oSQLHelper.SetCommandText(@" UPDATE VFolder" +
                                          @" SET Name = @Name" +
                                          @" ,RelativePath = @RelativePath" +
                                          @" ,RemoteId = @RemoteId" +
                                          @" ,CustomInfo = @CustomInfo" +
                                          @" WHERE Id_Folder = @FolderId");
            }

            oSQLHelper.SetCommandParameter("@FolderId", DbType.String, 36, this.ElementId.ToString());
            oSQLHelper.SetCommandParameter("@RepositoryId", DbType.Int32, null, RepositoryId);
            oSQLHelper.SetCommandParameter("@Name", DbType.String, 500, this.CurrentName);
            oSQLHelper.SetCommandParameter("@RelativePath", DbType.String, 200, this.PathRelative);
            oSQLHelper.SetCommandParameter("@RemoteId", DbType.String, 200, this.RemoteID);
            oSQLHelper.SetCommandParameter("@CustomInfo", DbType.String, 200, this.CustomProperties);
            oSQLHelper.ExecuteNonQuery();

            oSQLHelper.Dispose();
        }
    }
}
