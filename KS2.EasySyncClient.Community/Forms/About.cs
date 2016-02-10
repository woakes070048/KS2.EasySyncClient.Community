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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace KS2.EasySyncClient
{
    public partial class About : Form
    {
        public About()
        {
            InitializeComponent();
            linkLabel1.LinkClicked += linkLabel1_LinkClicked;

            label1.Text = "EasySync v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.ks2.fr/produits/ks2-easysync-client");
        }
    }
}
