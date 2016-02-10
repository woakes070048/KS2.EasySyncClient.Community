/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace KS2.EasySyncClient
{
    public partial class StoppingForm : System.Windows.Forms.Form
    {
        public bool AllowClosing
        {
            get;
            set;
        }

        public StoppingForm()
        {
            InitializeComponent();
            AllowClosing = false;
        }

        private void StoppingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!AllowClosing)
            {
                e.Cancel = true;
			}
		}
    }
}
