using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using eQuran;
using System.Globalization;
using System.Net;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Collections.Specialized;
using System.Reflection;

namespace eQuran
{
    public partial class Form1 : Form {

        private XmlDocument xmlpnla,xmlpnlb;       
        private enum DirectionEnum { Left, Right };
        const string wc = "wcg,wci,wct,wck";
        static readonly string[] hadeeth_xml = { "data/hadeeth_bokhary.xml",
                                       "data/hadeeth_muslim.xml",
                                       "data/sonan_dawood.xml",
                                       "data/sonan_eltarmazy.xml",
                                       "data/sonan_elnasaey.xml",
                                       "data/sonan_maga.xml"
                                     };
        string PATH_APP_DATA, PATH_AUDIO_TEMP;

        const string DOOWNLOAD_AUDIO_ERROR = "An Error occurred while retrieving the audio file, Check your internet connection status.";
        const string DOWNLOAD_TRANSLATION_ERROR = "An Error occurred while retrieving the online translation, Check your internet connection status.";
        

        frmAbout ffrmAbout;
        PanelEx downPanel;
        List<string> lstPlayList = new List<string>();
        /* dctReciters is used to hold a Key/Value list with all the available
           reciters and their corresponding URLs */
        Dictionary<string,string> dctReciters = new Dictionary<string,string>();
        string colorTheme = "";
        XmlDocument fXmlHadeeth;
        string hadeeth_loaded = "";
        bool[] isIdentityLoaded = new bool[2];
        BackgroundWorker bwAudio, bwText;

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern long mciSendString(string strCommand, StringBuilder strReturn, int iReturnLength, IntPtr hwndCallback);

        public class tafseerStruct {
            public string title;
            public string location;
            public string id;
            public string dir;

            public override string ToString(){
                return title;
            }
        }

        public class BackgroundWorkerTextArgument {
            public PanelEx Panel;
            public string Text;

            public BackgroundWorkerTextArgument(PanelEx Panel, string Text) {
                this.Panel = Panel;
                this.Text = Text;
            }

        }

        public Form1() {
            InitializeComponent();

            bwAudio = new BackgroundWorker();
            bwAudio.WorkerSupportsCancellation = true;
            bwAudio.DoWork +=new DoWorkEventHandler(bwAudio_DoWork);
            bwAudio.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwAudio_RunWorkerCompleted);

            bwText = new BackgroundWorker();
            bwText.DoWork += new DoWorkEventHandler(bwText_DoWork);
            bwText.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwText_RunWorkerCompleted);


            PATH_APP_DATA  = Assembly.GetExecutingAssembly().Location;
            PATH_APP_DATA = PATH_APP_DATA.Replace("eQuran.exe", @"data\\");
            PATH_AUDIO_TEMP = Environment.GetEnvironmentVariable("TEMP") + 
                                @"\qaudio.mp3";

        }



        
        /* Hadeeth procedures and functions */
        private void LoadHadeethXml(string FileXml ) {

            /* do nothing if el katab is already loaded */
            if (hadeeth_loaded == FileXml)
                return;
            else {
                
                fXmlHadeeth = new XmlDocument();
                fXmlHadeeth.Load(FileXml);
                hadeeth_loaded = FileXml;

            }            
            
        }

        private void LoadBab(string BabName) {

            if (fXmlHadeeth == null) return;

            XmlNode nodeBab, nodeHadeeth;

            nodeBab = fXmlHadeeth.SelectSingleNode("//bab[@name='" + BabName + "']");
            
            if (nodeBab != null) {

                hdtViewer.Entries.Clear();
                for (int i = 0; i < nodeBab.ChildNodes.Count; i++) {
                    nodeHadeeth = nodeBab.ChildNodes[i];
                    hdtViewer.Entries.Add( nodeHadeeth.InnerText.Trim(), 
                                          nodeHadeeth.Attributes[0].Value);
                                            
                }
                //hdtViewer.Invalidate();
            }
        }

        private void trvHadeeth_AfterSelect(object sender, TreeViewEventArgs e) {

            /* Only one book is loaded at a time, depending on what
             *  the user is browsing for */
             
            if (trvHadeeth.SelectedNode != null) {
                if (trvHadeeth.SelectedNode.Level > 0) {
                    
                    TreeNode rNode = trvHadeeth.SelectedNode; ;
                    if (hdtViewer.ShowSource) hdtViewer.ShowSource = false;

                    while (rNode.Parent != null) rNode = rNode.Parent;
                    LoadHadeethXml(hadeeth_xml[rNode.Index]);
                    LoadBab(trvHadeeth.SelectedNode.Text);

                }
            }
        }

        /* Read all el 2a7dees al nabawaya and fill the treeview */
        private void LoadHadeethTree() {

            /* Check first if the tree was already loaded*/
            if (trvHadeeth.Nodes.Count > 0) return;

            XmlTextReader xmlReader;
            TreeNode rNode, bNode;

            for (int i = 0; i < 6; i++) {

                /* Check if the file exists first*/
                if (!File.Exists(hadeeth_xml[i])) continue;
                xmlReader = new XmlTextReader(hadeeth_xml[i]);               
                xmlReader.ReadToFollowing("kotob");
                rNode = new TreeNode(xmlReader.GetAttribute(0));                
                
                xmlReader.ReadToFollowing("katab");                                
                do {
                    xmlReader.MoveToFirstAttribute();
                    bNode = new TreeNode(xmlReader.ReadContentAsString());
                    xmlReader.MoveToElement();
                    if (xmlReader.ReadToDescendant("bab")) {
                        /* iterate through el babs in el katab if there are any */
                        do {
                            xmlReader.MoveToAttribute(0);
                            bNode.Nodes.Add(xmlReader.ReadContentAsString());
                        } while (xmlReader.ReadToNextSibling("bab"));
                    }
                    rNode.Nodes.Add(bNode);
                } while (xmlReader.ReadToFollowing("katab"));


                trvHadeeth.Nodes.Add(rNode);
               
            
            }

        }
        

        private string RemoveHtml(string r,string rtype) {
            Regex rreg = new Regex("<[^>]*>");
            int rbegin;
            
            if ((rtype == "wcg")|| (rtype == "wct")) {
                rbegin = r.IndexOf("<p>");
                if (rbegin != -1) {
                    rbegin += 3;
                    r = r.Substring(rbegin, r.LastIndexOf("</p>") - rbegin);
                }
                else r = "No Additional Commentary Available";

            }
            else if ((rtype == "wck") || (rtype == "wci")) {
                rbegin = r.IndexOf("<p>");
                if (rbegin != -1) {
                    rbegin += 3;
                    r = r.Substring(rbegin, r.LastIndexOf("</p>") - rbegin);
                }
                else r = "No Additional Commentary Available";
            }
            return (rreg.Replace(r, " "));

        }


        public string RemoveDiacritics(string stIn) {
            string stFormD = stIn.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            for (int ich = 0; ich < stFormD.Length; ich++) {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[ich]);
                if (uc != UnicodeCategory.NonSpacingMark) {
                    sb.Append(stFormD[ich]);
                }
            }

            return (sb.ToString());
        }


      
        private void rItem_Click(object sender, EventArgs e) {
            
            ToolStripMenuItem rsender = (ToolStripMenuItem)sender;
            tafseerStruct rstruct = (tafseerStruct)rsender.Tag;

            downPanel.SelectedText = rstruct.title;
            
            XmlDocument rxml;
            rxml = (downPanel.Name == "pnla") ? xmlpnla : xmlpnlb;

            if (!wc.Contains(rstruct.id)) {
                rxml.Load(rstruct.location);
            }             
            
            ShowTrans(downPanel);

        }
        
        private void pnl_MoveDown(object sender ) {
            PanelEx pnl = (PanelEx)sender;
            int sItem = 0;
            downPanel = pnl;

            for (int i = 0; i < mextension.Items.Count; i++) {
               ((ToolStripMenuItem)mextension.Items[i]).Checked = false;
               if (((ToolStripMenuItem)mextension.Items[i]).Text ==
                   pnl.SelectedText) sItem = i;
            }
            
            ((ToolStripMenuItem)mextension.Items[sItem]).Checked = true;
            
            mextension.Show(pnl,
                new Point(pnl.Width - mextension.Width,
                            pnl.DisplayRectangle.Top));

        }

        private void pnl_MoveLeft(object sender) {
            MoveToTafseer((PanelEx)sender,DirectionEnum.Left);
        }

        private void pnl_MoveRight(object sender) {
            MoveToTafseer((PanelEx)sender,DirectionEnum.Right);
        }

        private void MoveToTafseer(PanelEx pnl, DirectionEnum dir) {
            int citem = 0; 
            tafseerStruct rstruct;

            for (int i = 0; i < mextension.Items.Count; i++) {
                
                rstruct = (tafseerStruct)mextension.Items[i].Tag;
                if (rstruct.title == pnl.SelectedText){
                    if (dir == DirectionEnum.Left) {
                        citem = i - 1;
                        if (i == 0) citem = mextension.Items.Count - 1;
                    }
                    else if (dir == DirectionEnum.Right) {
                        citem = i + 1;
                        if (i == mextension.Items.Count - 1) citem = 0;
                    }

                    break;
                }
            }
            rstruct = (tafseerStruct)mextension.Items[citem].Tag;
            pnl.SelectedText = rstruct.title;
            string wc = "wcg,wci,wct,wck";

            
            XmlDocument rxml;
            rxml = (pnl.Name == "pnla") ? xmlpnla : xmlpnlb;

            if (!wc.Contains(rstruct.id)) {
                rxml.Load(rstruct.location);
            } 
            ShowTrans(pnl);

        }

        private void ShowTrans(PanelEx rpanel) {

            if (qv.SelectedIndex != -1) {
                TextBoxEx txtpnl = (TextBoxEx)rpanel.Controls[0];
                tafseerStruct r = GettafseerFromTitle(rpanel.SelectedText);
                txtpnl.RightToLeft = (r.dir == "rtl") ? RightToLeft.Yes : RightToLeft.No;
                
                if (wc.Contains(r.id)) { /* retreive from the web */
                    txtpnl.ShowLoading();
                    lblstatus.Text = "Retrieving Content From Web...";                    
                    string rpath = (r.location + qv.SelectedAya.ID + ".html");

                    BackgroundWorker bwText = new BackgroundWorker();
                    bwText.DoWork += new DoWorkEventHandler(bwText_DoWork);
                    bwText.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwText_RunWorkerCompleted);

                    bwText.RunWorkerAsync(new BackgroundWorkerTextArgument(
                                           rpanel,rpath ));

                }
                else { /* reteieve local translations */
                    XmlDocument rxml;
                    rxml = (rpanel.Name == "pnla") ? xmlpnla : xmlpnlb;
                    XmlNode x = rxml.SelectSingleNode("//AYA[@id='" + qv.SelectedAya.ID + "']");
                    if (x != null) txtpnl.Text = x.InnerText;
                }
            }


        }

        private tafseerStruct GettafseerFromTitle(string p) {
            tafseerStruct r = (tafseerStruct)mextension.Items[0].Tag;

            for (int i = 0; i < mextension.Items.Count; i++) {
                r = (tafseerStruct)mextension.Items[i].Tag;
                if (r.title == p) break;
            }

            return r;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {


            if (WindowState == FormWindowState.Normal) {
                Properties.Settings.Default.FormLocation = Location;
                Properties.Settings.Default.FormSize = Size;
            }
            else {
                Properties.Settings.Default.FormLocation = RestoreBounds.Location;
                Properties.Settings.Default.FormSize = RestoreBounds.Size;            
            }

            /* Save Quran Identity State if it was loaded */
            if (isIdentityLoaded[0]) {
                Properties.Settings.Default.CurrentSoura = cmbSoura.SelectedIndex;
                Properties.Settings.Default.CurrentAya = qv.SelectedIndex;
                Properties.Settings.Default.CurrentScrollValue = qv.ScrollPosition;
                Properties.Settings.Default.SearchState = cntright.Panel2Collapsed;
                Properties.Settings.Default.quranReciter = cmbrecitors.SelectedIndex;
                Properties.Settings.Default.VolumeLevel = QuranVolume.Position;
                Properties.Settings.Default.FontName = cmbFontName.Text;
                Properties.Settings.Default.FontSize = cmbFontSize.Text;
                Properties.Settings.Default.ViewMode =
                    (qv.ViewMode == qViewer.ViewModeFlags.SingleLine) ?
                    0 : 1;
                Properties.Settings.Default.pnlaSelected = pnla.SelectedText;
                Properties.Settings.Default.pnlbSelected = pnlb.SelectedText;
            }

            /* Save Hadeeth Identity State if it was loaded */
            if (isIdentityLoaded[1]) {
                if (trvHadeeth.SelectedNode != null)
                    Properties.Settings.Default.hadeethSelected =
                            trvHadeeth.SelectedNode.FullPath;

                if (cmbhdtSearch.Items.Count > 0) {
                    StringCollection hadeethSearchHistory = Properties.Settings.Default.hadeethSearchHistory;
                    for (int i = 0; i < cmbhdtSearch.Items.Count; i++) {
                        if (!hadeethSearchHistory.Contains((string)cmbhdtSearch.Items[i]))
                            hadeethSearchHistory.Add((string)cmbhdtSearch.Items[i]);
                    }
                }

            }

            Properties.Settings.Default.Identity =
                (picHeader1.SelectedIdentity == IdentityEnum.Quran) ?
                "Quran" : "Hadeeth";            
            Properties.Settings.Default.ColorTheme = colorTheme;

            Properties.Settings.Default.Save();

        }



        private void LoadIdentity(IdentityEnum Identity) {

            switch (Identity) {
                case IdentityEnum.Quran:

                    if (!isIdentityLoaded[0]) {
                        XmlTextReader xmlReader;

                        /* Read the list of all the available fonts */
                        FontFamily[] ff = FontFamily.Families;
                        for (int i = 0; i < ff.Length; i++) {
                            cmbFontName.Items.Add((string)ff[i].Name);
                        }
                        cmbFontName.Text = Properties.Settings.Default.FontName;
                        cmbFontSize.Text = Properties.Settings.Default.FontSize;


                        /* Read and Load Extensions */
                        xmlReader = new XmlTextReader(PATH_APP_DATA + "extensions.xml");
                        tafseerStruct rstruct;
                        xmlReader.ReadToFollowing("extension");
                        do {
                            ToolStripItem rItem;
                            rstruct = new tafseerStruct();
                            rstruct.dir = xmlReader.GetAttribute("dir");
                            rstruct.id = xmlReader.GetAttribute("id");
                            xmlReader.ReadToFollowing("title");
                            rstruct.title = xmlReader.ReadElementString();
                            xmlReader.ReadToFollowing("location");
                            rstruct.location = xmlReader.ReadElementString();

                            rItem = mextension.Items.Add(rstruct.title);
                            rItem.Tag = rstruct;
                            rItem.Click += new EventHandler(rItem_Click);

                        } while (xmlReader.ReadToFollowing("extension"));
                        xmlReader.Close();

                        /* Read all the available recitors */
                        xmlReader = new XmlTextReader(PATH_APP_DATA + "recitations.xml");
                        xmlReader.ReadToFollowing("recitation");
                        do {
                            dctReciters.Add(xmlReader.GetAttribute("name"),
                                            xmlReader.GetAttribute("url"));
                            cmbrecitors.Items.Add(xmlReader.GetAttribute("name"));
                        } while (xmlReader.ReadToNextSibling("recitation"));
                        xmlReader.Close();
                        cmbrecitors.SelectedIndex = Properties.Settings.Default.quranReciter;
                            
                        /* Read all the sowar names from the xml file */
                        xmlReader = new XmlTextReader(PATH_APP_DATA + "quran.xml");
                        xmlReader.ReadToFollowing("SOURA");
                        do {
                            cmbSoura.Items.Add(xmlReader.GetAttribute("id") + "."
                                            + xmlReader.GetAttribute("name"));
                        } while (xmlReader.ReadToNextSibling("SOURA"));
                        xmlReader.Close();


                        /* Load the quran to the viewer */
                        qv.LoadXmlFile(PATH_APP_DATA + "quran.xml");
                        qv.LoadQuranParts(PATH_APP_DATA + "quran_parts.xml");

                        /* Read other UI elements settings for the quran identity*/
                        cntright.Panel2Collapsed = Properties.Settings.Default.SearchState;
                        chkSearch.Checked = !cntright.Panel2Collapsed;
                        pnla.SelectedText = Properties.Settings.Default.pnlaSelected;
                        pnlb.SelectedText = Properties.Settings.Default.pnlbSelected;
                        QuranVolume.Position = Properties.Settings.Default.VolumeLevel;


                        xmlpnla = new XmlDocument(); xmlpnlb = new XmlDocument();
                        if (!wc.Contains(GettafseerFromTitle(pnla.SelectedText).id))
                            xmlpnla.Load(GettafseerFromTitle(pnla.SelectedText).location);
                        if (!wc.Contains(GettafseerFromTitle(pnlb.SelectedText).id))
                            xmlpnlb.Load(GettafseerFromTitle(pnlb.SelectedText).location);

                        if (Properties.Settings.Default.ViewMode == 0) {
                            qv.ViewMode = qViewer.ViewModeFlags.SingleLine;
                            chkSingleline.Checked = true;
                        }
                        else {
                            qv.ViewMode = qViewer.ViewModeFlags.MultiLine;
                            chkMultiline.Checked = true;
                        }
                        cmbSoura.SelectedIndex = Properties.Settings.Default.CurrentSoura;
                        qv.SelectedIndex = Properties.Settings.Default.CurrentAya;
                        qv.ScrollPosition = Properties.Settings.Default.CurrentScrollValue;

                    }
                    spcHadeeth.Visible = false;
                    spcQuran.Visible = true;
                    pnlQuranTools.Show();
                    pnlHadeethTools.Hide();
                    isIdentityLoaded[0] = true;

                    break;
                case IdentityEnum.Hadeeth:

                    string hadeethSelected = Properties.Settings.Default.hadeethSelected;
                    StringCollection hadeethSearchHistory = Properties.Settings.Default.hadeethSearchHistory;

                    if (!isIdentityLoaded[1]) { /* Identity is loaded for the first time */
                        LoadHadeethTree();
                        
                        if (hadeethSearchHistory != null)
                            foreach (string rItem in hadeethSearchHistory)
                                cmbhdtSearch.Items.Add(rItem);
                        
                        cmbhdtSearchScope.Items.Add("جميع الكتب");
                        cmbhdtSearchScope.SelectedIndex = 0;
                        for (int i = 0; i < 6; i++)
                            cmbhdtSearchScope.Items.Add(trvHadeeth.Nodes[i].Text);
                        
                        trvHadeeth.SelectedNode = NodeFromIndex(trvHadeeth ,hadeethSelected);                           
                        trvHadeeth.Select();                               

                       
                    }
                    spcHadeeth.Visible = true;
                    spcQuran.Visible = false;
                    pnlQuranTools.Hide();
                    pnlHadeethTools.Show();
                    isIdentityLoaded[1] = true;
                    break;

            }


        }

        /* WinForms TreeView doesn't have any method for searching a node
         * given its index, therefore i used the recursive calls */
        bool isNodeFound = false;
        TreeNode NodeFound;

        private void NodeFromIndexRecursive(TreeNode treeNode, string FullPath) {

            if (isNodeFound) return;
            foreach (TreeNode tn in treeNode.Nodes) {
                if (tn.FullPath == FullPath) {
                    isNodeFound = true;
                    NodeFound = tn;
                    break;
                }
                NodeFromIndexRecursive(tn, FullPath);
            }
        }

        private TreeNode NodeFromIndex(TreeView treeView, string FullPath) {

            TreeNodeCollection nodes = treeView.Nodes;
            isNodeFound = false; NodeFound = null;
            
            foreach (TreeNode n in nodes) {
                if (n.FullPath == FullPath) {
                    NodeFound = n;
                    isNodeFound = true;
                    break;
                }
                NodeFromIndexRecursive(n, FullPath);
            }

            return NodeFound;
        }



        
        private void Form1_Load(object sender, EventArgs e) {
       
            /* Restore previous location and size */
            Location = Properties.Settings.Default.FormLocation;
            Size = Properties.Settings.Default.FormSize;           
            
            /* Setting the color theme */
            colorTheme = Properties.Settings.Default.ColorTheme;
            if (colorTheme == "gigi") lblcolorgigi_Click(null, null);
            else if (colorTheme == "emerald") lblcoloremerald_Click(null,null);
            else if (colorTheme == "bday") lblcolorbday_Click(null, null);
            else if (colorTheme == "green") lblcolorgreen_Click(null, null);
            
            /* Loading the identity */
            if (Properties.Settings.Default.Identity == "Quran") 
                 picHeader1.SelectedIdentity = IdentityEnum.Quran;     
            else picHeader1.SelectedIdentity = IdentityEnum.Hadeeth;            
            picHeader1_SelectedIdentityChanged(this, new EventArgs());

        }

        private void mAbout_Click(object sender, EventArgs e) {
            if (ffrmAbout == null) ffrmAbout = new frmAbout();
            ffrmAbout.ShowDialog();
        }

        private void cmbSoura_SelectedIndexChanged(object sender, EventArgs e) {
            string re = (string)cmbSoura.SelectedItem;
            re = re.Substring(re.IndexOf('.') + 1);
            qv.SelectedSoura = re;            
        }

        private void cmdsearch_Click(object sender, EventArgs e) {
            if (txtsearch.Text == "") return;

            XmlDocument sdata = new XmlDocument();
            XmlNodeList sresult;
            ListViewItem x;

            lstsearch.Items.Clear();
            sdata.Load(PATH_APP_DATA + "quran/quran_search.xml");


            sresult = sdata.SelectNodes(
                    "//*[contains(text(),'" + txtsearch.Text + "')]");

            for (int i = 0; i < sresult.Count; i++) {
                x = new ListViewItem();
                x.Text = sresult[i].ParentNode.Attributes[0].InnerText;
                x.SubItems.Add(sresult[i].ParentNode.Attributes[1].InnerText);
                x.SubItems.Add(sresult[i].Attributes[0].InnerText.Substring(3));
                x.SubItems.Add(sresult[i].InnerText);
                lstsearch.Items.Add(x);
            }
            lblresultcount.Text = lstsearch.Items.Count.ToString() + " occurrence(s) found."; 

        }
        
        private void lstsearch_DoubleClick(object sender, EventArgs e) {
            ListView.SelectedListViewItemCollection rItem;
            rItem = lstsearch.SelectedItems;
           
            cmbSoura.SelectedIndex = int.Parse(rItem[0].Text) - 1;
            qv.SelectedIndex = int.Parse(rItem[0].SubItems[2].Text) ;
        }

        private void lstsearch_Resize(object sender, EventArgs e) {
            lstsearch.Columns[3].Width = lstsearch.Width - 5 -
                                        (lstsearch.Columns[0].Width +
                                         lstsearch.Columns[1].Width +
                                         lstsearch.Columns[2].Width);
                                         
        }

        private void bfontname_SelectedIndexChanged(object sender, EventArgs e) {
            ChangeFont();
        }
        private void bfontsize_SelectedIndexChanged(object sender, EventArgs e) {
            ChangeFont();
        }

        private void ChangeFont() {
            
            string fontName = cmbFontName.Text;
            int fontSize = (cmbFontSize.Text.Length == 0) ? 14 : int.Parse(cmbFontSize.Text);

            
            try {
                qv.Font = new Font(fontName , fontSize);
                cmbFontName.BackColor = Color.White;
            }
            catch (ArgumentException) {
                cmbFontName.BackColor = Color.LightCoral;
            }
        }

        private void lblabout_Click(object sender, EventArgs e) {
            frmAbout fAbout = new frmAbout();
            fAbout.ShowDialog();

        }
        
        protected override void WndProc(ref Message rMessage) {
            //a hook used when playing the whole soura, the method helps to know whenever
            //an aya finsihed to begin the next one
            //const int MCI_NOTIFY_SUCCESSFUL = 0x1;
            if ((rMessage.Msg == 0x3B9) && 
                (rMessage.WParam.ToInt32() == 0x1)) {
                mciSendString("close AyaFile", null, 0, IntPtr.Zero);
                lblnowplaying.Text ="";
                Debug.Print("Play Completed " + lstPlayList[0]);    
                
                if (lstPlayList.Count > 1) {
                    lstPlayList.RemoveAt(0); //remove just played element from the list
                    Debug.Print("Play Next.");
                    playStart();
                }
            }
            base.WndProc(ref rMessage);
        }


        private void qv_ItemClick(string ItemID) {

            ShowTrans(pnla); 
            ShowTrans(pnlb);

        }
        
        private void qv_AyaSoundClick(Aya rAya) {
            lstPlayList.Clear();
            lstPlayList.Add(rAya.ID);            
            lblnowplaying.Tag = rAya.ParentSoura.Name;

            playStart();
        }

        private void qv_SouraSoundClick(Soura rSoura) {
            
            lstPlayList.Clear();
            for (int i = 0; i < rSoura.AyasCount; i++) {
                lstPlayList.Add(rSoura.Ayas[i].ID);
            }

            // Save the name of the soura to display it later
            lblnowplaying.Tag = rSoura.Name;
            playStart();                        
        }

        private void playStart() {

            string remotepath = string.Format( "{0}{1}/{2}.mp3",
                            dctReciters[cmbrecitors.Text],
                            lstPlayList[0].Substring(0,3),
                            lstPlayList[0].Substring(3,3)
                            );
                 
            lblstatus.Text = "Retrieving Audio File...";
            Debug.Print("Downloading " + lstPlayList[0]);
            
            if (!bwAudio.IsBusy) bwAudio.RunWorkerAsync(remotepath);

        }

        private void bwAudio_DoWork(object sender, DoWorkEventArgs e) {

            Encoding defaultEncoding = Encoding.GetEncoding(1256);
            WebClient wbcDownload = new WebClient();
            Stream stDownload;
            StreamReader stReaderDownload;
            string textDownload;

            string audioPath = (string)e.Argument;

            try {
                wbcDownload.DownloadFile(new Uri(audioPath), PATH_AUDIO_TEMP);
            }
            catch (Exception) {
                e.Result = -1;
            }
            
        }

        private void bwAudio_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {

            int r = 0;
            if (e.Result != null) r = (int)e.Result;
            
            if (e.Cancelled) 
                r = -1;

            if (r == -1) {
                lblstatus.Text = DOOWNLOAD_AUDIO_ERROR;
            }
            else {
                lblstatus.Text = "Downloaded Audio File Successfully";
                string openstr = "open \"" + PATH_AUDIO_TEMP + "\" alias AyaFile";
                string volumestr = String.Format("setaudio AyaFile volume to {0}", QuranVolume.Position * 10);
                string playstr = "play AyaFile notify";

                Debug.Print("Download Compelete, Playing " + lstPlayList[0]);
                mciSendString(openstr, null, 0, IntPtr.Zero);
                mciSendString(volumestr, null, 0, IntPtr.Zero);
                mciSendString(playstr, null, 0, this.Handle);
                
                lblnowplaying.Text = string.Format("Playing {0} - Aya {1}",
                                                    (string)lblnowplaying.Tag, lstPlayList[0].Substring(3));

                //mciSendString(string.Format("play \"{0}\" notify", localpath), null, 0, this.Handle);

            }
        }

        private void bwText_DoWork(object sender, DoWorkEventArgs e) {

            Encoding defaultEncoding = Encoding.GetEncoding(1256);
            WebClient wbcDownload = new WebClient();
            Stream stDownload;
            StreamReader stReaderDownload;
            BackgroundWorkerTextArgument rArgument = ((BackgroundWorkerTextArgument)e.Argument);
            string textDownload;

            wbcDownload.Encoding = defaultEncoding;

            try {
                stDownload = wbcDownload.OpenRead(new Uri(rArgument.Text));
                stReaderDownload = new StreamReader(stDownload, defaultEncoding);
                textDownload = stReaderDownload.ReadToEnd();
                stReaderDownload.Close();
                stDownload.Close();
                                             
                e.Result = new BackgroundWorkerTextArgument(rArgument.Panel, textDownload);
            }
            catch (Exception) {
                e.Result = new BackgroundWorkerTextArgument(rArgument.Panel, "-1");
            }
            
        }

        private void bwText_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {

            BackgroundWorkerTextArgument rArgument = ((BackgroundWorkerTextArgument)e.Result);
            PanelEx rpanel = rArgument.Panel;
            TextBoxEx txtpnl = (TextBoxEx)rpanel.Controls[0];
            tafseerStruct r = GettafseerFromTitle(rpanel.SelectedText);

            if (rArgument.Text == "-1") {
                lblstatus.Text = DOWNLOAD_TRANSLATION_ERROR;
                txtpnl.HideLoading();
                txtpnl.Text = "";
                return;
            }

            txtpnl.RightToLeft = (r.dir == "rtl") ? RightToLeft.Yes : RightToLeft.No;
            string rText = ((BackgroundWorkerTextArgument)e.Result).Text;
            txtpnl.Text = RemoveDiacritics(RemoveHtml(rText, r.id)); 
            lblstatus.Text = "Ready";
            txtpnl.HideLoading();

        }


        private void lblcolorgigi_Click(object sender, EventArgs e) {
            clrPainter.HeaderColor = Color.FromArgb(157, 196, 91);
            clrPainter.BarColor = Color.FromArgb(196, 91, 125);
            clrPainter.HeaderText = Color.FromArgb(252, 252, 252);
            lblabout.BackColor = clrPainter.HeaderColor;
            pnlQuranTools.BackColor = clrPainter.BarColor;
            pnlHadeethTools.BackColor = clrPainter.BarColor;
            colorTheme = "gigi";
        }

        private void lblcolorbday_Click(object sender, EventArgs e) {
            clrPainter.HeaderColor = Color.FromArgb(252, 77, 34);
            clrPainter.BarColor = Color.FromArgb(119, 79, 56);
            clrPainter.HeaderText = Color.FromArgb(250, 247, 207);
            lblabout.BackColor = clrPainter.HeaderColor;
            pnlQuranTools.BackColor = clrPainter.BarColor;
            pnlHadeethTools.BackColor = clrPainter.BarColor;
            colorTheme = "bday";
        }

        private void lblcolorgreen_Click(object sender, EventArgs e) {
            clrPainter.HeaderColor = Color.FromArgb(40, 85, 51);
            clrPainter.BarColor = Color.FromArgb(26, 54, 26);
            clrPainter.HeaderText = Color.White;
            lblabout.BackColor = clrPainter.HeaderColor;
            pnlQuranTools.BackColor = clrPainter.BarColor;
            pnlHadeethTools.BackColor = clrPainter.BarColor;
            colorTheme = "green";
        }

        private void lblcoloremerald_Click(object sender, EventArgs e) {
            clrPainter.HeaderColor = Color.FromArgb(0, 136, 255);
            clrPainter.BarColor = Color.FromArgb(66, 70, 73);
            clrPainter.HeaderText = Color.FromArgb(248, 252, 255);
            lblabout.BackColor = clrPainter.HeaderColor;
            pnlQuranTools.BackColor = clrPainter.BarColor;
            pnlHadeethTools.BackColor = clrPainter.BarColor;
            colorTheme = "emerald";
        }

        private void QuranVolume_VolumeChanged(object Sender) {
            string vCmd = String.Format("setaudio AyaFile volume to {0}", QuranVolume.Position * 10);
            mciSendString(vCmd, null, 0, IntPtr.Zero);
        }

        private void cmdstop_Click(object sender, EventArgs e) {
            lstPlayList.Clear();
            lblnowplaying.Text = "Stopped";
            mciSendString("stop AyaFile", null, 0, IntPtr.Zero);
            mciSendString("close AyaFile", null, 0, IntPtr.Zero);

        }

        private void cmdpause_Click(object sender, EventArgs e) {
            mciSendString("pause AyaFile", null, 0, IntPtr.Zero);

        }

        private void cmdplay_Click(object sender, EventArgs e) {
            mciSendString("resume AyaFile", null, 0, IntPtr.Zero);

        }

        private void chkSearch_CheckedChanged(object sender, EventArgs e) {
            cntright.Panel2Collapsed = !cntright.Panel2Collapsed;
            txtsearch.Focus();
        }


        private void chkMultiline_Click(object sender, EventArgs e) {
            qv.ViewMode = qViewer.ViewModeFlags.MultiLine;
            chkMultiline.Checked = true;
            chkSingleline.Checked = false;
        }


        private void chkSingleline_Click(object sender, EventArgs e) {
            qv.ViewMode = qViewer.ViewModeFlags.SingleLine;
            chkMultiline.Checked = false;
            chkSingleline.Checked = true;

        }

        private void picHeader1_SelectedIdentityChanged(object sender, EventArgs e) {

            switch (picHeader1.SelectedIdentity) {
                case IdentityEnum.Quran:
                    LoadIdentity(IdentityEnum.Quran);
                    break;
                case IdentityEnum.Hadeeth:
                    LoadIdentity(IdentityEnum.Hadeeth);
                    break;
            }

        }

        private void chkExpanded_Click(object sender, EventArgs e) {

            spcQuran.Panel1Collapsed = !spcQuran.Panel1Collapsed;
            qv.UpdateView();

        }

        private void cmdhdtSearch_Click(object sender, EventArgs e) {
            
            XmlNodeList hList;
            XmlNode hNode;
            /* I used an array of Xml */
            XmlDocument[] hXml = new XmlDocument[6];

            hdtViewer.Entries.Clear();
            hdtViewer.ShowSource = true;
            hdtViewer.Invalidate(); /*TODO: update HadeethViewer automaically whenever an item is cleared*/

            for (int i = 0; i < hadeeth_xml.Length; i++) {
                
                hXml[i] = new XmlDocument();
                if (hadeeth_xml[i] == hadeeth_loaded) hXml[i] = fXmlHadeeth;
                else hXml[i].Load(hadeeth_xml[i]);

                hList = hXml[i].SelectNodes(string.Format("//hadeeth[contains(./text(),'{0}')]",
                                               cmbhdtSearch.Text));

                for (int j = 0; j < hList.Count; j++) {
                    hNode = hList.Item(j);
                    hdtViewer.Entries.Add(hNode.InnerText.Trim(),
                                          hNode.Attributes[0].Value,
                                          hNode.ParentNode.ParentNode.ParentNode.Attributes[0].Value ,
                                          hNode.ParentNode.ParentNode.Attributes[0].Value ,
                                          hNode.ParentNode.Attributes[0].Value );

                }                
            
            }

            if (hdtViewer.Entries.Count > 0)    
                cmbhdtSearch.Items.Add(cmbhdtSearch.Text);



        }


    }


}