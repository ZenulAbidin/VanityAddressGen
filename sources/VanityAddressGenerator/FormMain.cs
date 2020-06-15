using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Info.Blockchain.API;
using Info.Blockchain.API.BlockExplorer;
using NBitcoin;

namespace VanityAddressGenerator
{
    public partial class FormMain : Form
    {
        private string _addressToSend = "address";
        private double _btcStored;

        const string WorkFolderPath = @"work";
        const string FilePathAddresses = WorkFolderPath + @"\vanityAddresses.txt";
        const string FilePathNotEmptyAddresses = WorkFolderPath + @"\notEmptyAddresses.txt";
        const string FilePathSecretKeys = WorkFolderPath + @"\addressSecretPairs.txt";

        // 1000    - 0.036MB
        // 10000   - 0.36MB
        // 100000  - 3.6MB
        // 1000000 - 36MB
        private int _addressCnt = 100;
        private readonly List<int> _fileSizesMb = new List<int> { 1000, 10000, 100000, 1000000 }; 

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            buttonNotEmptyAddresses.Enabled = false;
            buttonSend.Enabled = false;
            labelBtcStored.Text = _btcStored + @" BTC";
            labelBtcStored.Enabled = false;
            textBoxSendAddress.Enabled = false;
            pictureBoxAddressGenerating.Visible = false;
            pictureBoxBackgroundAddrGen.Visible = true;
            pictureBoxRefreshing.Visible = false;
            pictureBoxBackgroundWallet.Visible = true;

            labelStatusGenAddr.Text = "";

            _fileSizesMb.ForEach(x => comboBoxFileSize.Items.Add(x));
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            pictureBoxAddressGenerating.Visible = true;
            pictureBoxBackgroundAddrGen.Visible = false;
            buttonStart.Enabled = false;
            comboBoxFileSize.Enabled = false;
            labelStatusGenAddr.Text = @"Addresses are being generated...";

            backgroundWorkerStart.RunWorkerAsync();
        }

        private void buttonOpenAddressFolder_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(WorkFolderPath)) Directory.CreateDirectory(WorkFolderPath);

            Process.Start(WorkFolderPath);
        }

        private void backgroundWorkerStart_DoWork(object sender, DoWorkEventArgs e)
        {
            var keys = new HashSet<Key>();
            for (int i = 0; i < _addressCnt; i++)
            {
                var key = new Key();

                keys.Add(key);
            }

            if (!Directory.Exists(WorkFolderPath)) Directory.CreateDirectory(WorkFolderPath);
            if (File.Exists(FilePathAddresses)) File.Delete(FilePathAddresses);
            if(File.Exists(FilePathSecretKeys)) File.Delete(FilePathSecretKeys);
            using (StreamWriter sw = File.CreateText(FilePathAddresses))
            using (StreamWriter sw2 = File.CreateText(FilePathSecretKeys))
            {
                foreach (var key in keys)
                {
                    string addr = key.PubKey.GetAddress(Network.Main).ToString();
                    sw.WriteLine(addr);
                    sw2.WriteLine(addr + ":" + key.GetBitcoinSecret(Network.Main));
                }
                sw.Flush();
                sw2.Flush();
            }
            
            var sortedAddresses = new SortedSet<string>();
            var sortedSecretKeys = new SortedSet<string>();
            foreach (var line in File.ReadAllLines(FilePathAddresses))
            {
                sortedAddresses.Add(line);
            }
            foreach (var line in File.ReadAllLines(FilePathSecretKeys))
            {
                sortedSecretKeys.Add(line);
            }
            if (File.Exists(FilePathAddresses)) File.Delete(FilePathAddresses);
            if(File.Exists(FilePathSecretKeys)) File.Delete(FilePathSecretKeys);
            using (StreamWriter sw = File.CreateText(FilePathAddresses))
            using (StreamWriter sw2 = File.CreateText(FilePathSecretKeys))
            {
                foreach (var a in sortedAddresses)
                    sw.WriteLine(a);
                foreach (var s in sortedSecretKeys)
                    sw2.WriteLine(s);

                sw.Flush();
                sw2.Flush();
            }

        }

        private void backgroundWorkerStart_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            buttonStart.Enabled = true;
            comboBoxFileSize.Enabled = true;
            pictureBoxAddressGenerating.Visible = false;
            pictureBoxBackgroundAddrGen.Visible = true;
            labelStatusGenAddr.Text = @"Address generation is finished.";
        }

        private void comboBoxFileSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            _addressCnt = (int) comboBoxFileSize.SelectedItem;
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            labelBtcStored.Enabled = false;
            textBoxSendAddress.Enabled = false;
            buttonNotEmptyAddresses.Enabled = false;
            buttonSend.Enabled = false;
            buttonRefresh.Enabled = false;
            pictureBoxRefreshing.Visible = true;
            pictureBoxBackgroundWallet.Visible = false;

            backgroundWorkerRefresh.RunWorkerAsync();
        }

        private void backgroundWorkerRefresh_DoWork(object sender, DoWorkEventArgs e)
        {
            _btcStored = GetStoredBtc();
        }

        private void backgroundWorkerRefresh_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            labelBtcStored.Enabled = true;

            if (_btcStored > 0)
            {
                /*
                textBoxSendAddress.Enabled = true;
                buttonSend.Enabled = true;
                */
                buttonNotEmptyAddresses.Enabled = true;
            }
            buttonRefresh.Enabled = true;
            pictureBoxRefreshing.Visible = false;
            pictureBoxBackgroundWallet.Visible = true;

            labelBtcStored.Text = _btcStored + @" BTC";
        }

        private double GetStoredBtc()
        {
            double storedBtc = 0;

            if (!Directory.Exists(WorkFolderPath)) Directory.CreateDirectory(WorkFolderPath);
            if (File.Exists(FilePathNotEmptyAddresses)) File.Delete(FilePathNotEmptyAddresses);
            using (StreamWriter sw = File.CreateText(FilePathNotEmptyAddresses))
            {
                try
                {
                    var blockExplorer = new BlockExplorer();
                    foreach (var a in File.ReadAllLines(FilePathAddresses))
                    {
                        long currAddrSatoshi = blockExplorer.GetAddress(a).FinalBalance;

                        if (currAddrSatoshi > 0)
                        {
                            storedBtc += (double)currAddrSatoshi / 100000000;
                            foreach (var pubsec in File.ReadAllLines(FilePathSecretKeys))
                            {
                                if (pubsec.StartsWith(a)) 
                                    sw.WriteLine(pubsec);
                            }
                            sw.Flush();
                        }
                    }
                }
                catch(APIException e)
                {
                    MessageBox.Show(this, @"Blockchain exception: " + e.Message);
                }
            }

            return storedBtc;
        }

        private void textBoxSendAddress_TextChanged(object sender, EventArgs e)
        {
            _addressToSend = textBoxSendAddress.Text;
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            if (!Tools.IsBtcAddress(_addressToSend))
            {
                MessageBox.Show(this, @"Invalid btc address");
                return;
            }

            labelBtcStored.Enabled = false;
            textBoxSendAddress.Enabled = false;
            buttonNotEmptyAddresses.Enabled = false;
            buttonSend.Enabled = false;
            buttonRefresh.Enabled = false;
            pictureBoxRefreshing.Visible = true;
            pictureBoxBackgroundWallet.Visible = false;

            backgroundWorkerSend.RunWorkerAsync();
        }

        private void backgroundWorkerSend_DoWork(object sender, DoWorkEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void backgroundWorkerSend_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            labelBtcStored.Enabled = true;

            buttonRefresh.Enabled = true;
            pictureBoxRefreshing.Visible = false;
            pictureBoxBackgroundWallet.Visible = true;

            
            Process.Start("https://blockchain.info/address/" + _addressToSend);
            _btcStored = 0;

            labelBtcStored.Text = _btcStored + @" BTC";
        }

        private void buttonNotEmptyAddresses_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(WorkFolderPath)) Directory.CreateDirectory(WorkFolderPath);

            Process.Start(WorkFolderPath);
        }
    }
}
