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
using System.Runtime.Serialization;
using System.Text;

namespace KS2.EasySync.Core
{
    [Serializable]
    [DataContract]
    public class RepositoryElement
    {
        [DataMember]
        public String ElementId;
        [DataMember]
        public RepositoryElementType ElementType;
        [DataMember]
        public string PathRelative;
        [DataMember]
        public bool IsIdentified = false;
        [DataMember]
        public Int64 FileSize;
        [DataMember]
        public string ElementName;
        [DataMember]
        public string CustomProperties;

        public RepositoryElement()
        {
        }
    }
}