//Copyright (C) 2012 Matthew Thornhill (mrmt32@ph-mb.com)

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; either version 2
//of the License, or (at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using pHM.DVBLib.TransportStream;
using pHM.DVBLib.Common;
using System.Collections.ObjectModel;
using DirectShowLib.Sample;
using System.Windows.Threading;
using System.ComponentModel;
using DirectShowLib.BDA;
using DirectShowLib;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace pHMb.TS_Demux
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private BDAGraphBuilder bdaGraphBuilder = null;

        public List<FirmwareFile> CompletedFileList { get; set; }
        bool _completedListHasChanged = true;
        public List<FirmwareFile> InProgressFileList { get; set; }

        public Dictionary<string, FirmwareFile> CompletedFiles { get; set; }
        public Dictionary<string, FirmwareFile> InProgressFiles { get; set; }

        public double TransponderRate { get; set; }
        public Dictionary<ushort, string> CurrentFiles { get; set; }
        public string CurrentFilesString
        {
            get
            {
                StringBuilder filesString = new StringBuilder();

                lock (CurrentFiles)
                {
                    int i = 0;
                    foreach (KeyValuePair<ushort, string> fileKvp in CurrentFiles)
                    {
                        if (i == 0)
                        {
                            filesString.AppendFormat("Pid-0x{0:X2}: {1}", fileKvp.Key, fileKvp.Value);
                        }
                        else
                        {
                            filesString.AppendFormat("; Pid-0x{0:X2}: {1}", fileKvp.Key, fileKvp.Value);
                        }

                        i++;
                    }
                }

                return filesString.ToString();
            }
        }

        DispatcherTimer timer = new DispatcherTimer();

        private Dictionary<ushort, MemoryStream> _currentFwPackets = new Dictionary<ushort, MemoryStream>();

        long _bytesInLastSecond = 0;

        string _extractFolder;

        /// <summary>
        /// Notify property changed
        /// </summary>
        /// <param name="propertyName">Property name</param>
        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            CurrentFiles = new Dictionary<ushort, string>();

            // Deal with settings updates
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            InitializeComponent();

            Logger.LogFolder = @"pH-Mb\FirmwareDownloader";
            Logger.LogFileFormat = @"FwDownloader-{0:yyyy-MM-dd_hh-mm-ss-tt}.log";

            CompletedFileList = new List<FirmwareFile>();
            InProgressFileList = new List<FirmwareFile>();

            CompletedFiles = new Dictionary<string, FirmwareFile>();
            InProgressFiles = new Dictionary<string, FirmwareFile>();

            listViewCompleted.ItemsSource = CompletedFileList;
            listViewInProgress.ItemsSource = InProgressFileList;

            if (Properties.Settings.Default.ExtractDirectory == "unset")
            {
                string extractDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                                            "Sky Firmware");

                if (!Directory.Exists(extractDir)) Directory.CreateDirectory(extractDir);

                Properties.Settings.Default.ExtractDirectory = extractDir;
                Properties.Settings.Default.Save();
            }

            txtTsLocation.Text = Properties.Settings.Default.ExtractDirectory;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            TransponderRate = (_bytesInLastSecond * 8) / 1000 / 1000;
            OnPropertyChanged("TransponderRate");
            OnPropertyChanged("CurrentFilesString");

            _bytesInLastSecond = 0;


            if (_completedListHasChanged)
            {
                listViewCompleted.ItemsSource = null;
                listViewCompleted.ItemsSource = CompletedFileList;
                _completedListHasChanged = false;
            }

            listViewInProgress.ItemsSource = null;
            listViewInProgress.ItemsSource = InProgressFileList;
        }   

        private void buttonBrowse_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog openDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            openDialog.SelectedPath = txtTsLocation.Text;

            if (openDialog.ShowDialog() == true)
            {
                txtTsLocation.Text = openDialog.SelectedPath;
            }
        }

        public void ProcessBuffer(byte[] buffer)
        {
            byte[] packetBuffer = new byte[188];
            using (MemoryStream tsStream = new MemoryStream(buffer))
            {
                while (tsStream.Position < tsStream.Length)
                {
                    _bytesInLastSecond += 188;
                    tsStream.Read(packetBuffer, 0, 188);
                    TransportStreamPacket tsPacket = PacketParsing.ParsePacket(packetBuffer);

                    if (tsPacket.packetID == 0x60 || tsPacket.packetID == 0x61)
                    {
                        if (_currentFwPackets.ContainsKey(tsPacket.packetID))
                        {
                            if (tsPacket.payload[0] == 0x00 && (tsPacket.payload[1] == 0xB5 || tsPacket.payload[1] == 0xB6))
                            {
                                // We have started a new packet
                                using (BinaryReaderEndian packetReader = new BinaryReaderEndian(_currentFwPackets[tsPacket.packetID], false))
                                {
                                    packetReader.DontDispose = true;
                                    packetReader.BaseStream.Seek(0, SeekOrigin.Begin);

                                    FwPacket fwPacket;
                                    try
                                    {
                                        fwPacket = ProcessNextFwPacket(packetReader);
                                    }
                                    catch (EndOfStreamException)
                                    {
                                        // Probably a better way of doing this! (When 0x00 0xB5 exists at the beginning of a packet naturaly)
                                        _currentFwPackets[tsPacket.packetID].Write(tsPacket.payload, 0, tsPacket.payload.Length);
                                        continue;
                                    }

                                    string fwName = string.Format("{0:X2}-{1:X2}-{2:X2}", fwPacket.VendorId, fwPacket.ModelId, fwPacket.FirmwareVersion);

                                    lock (CurrentFiles)
                                    {
                                        CurrentFiles[tsPacket.packetID] = string.Format("{0:X2}{1:X2}-r{2}", fwPacket.VendorId, fwPacket.ModelId, fwPacket.FirmwareVersion);
                                    }

                                    if (fwPacket.SectionType == 1 && !CompletedFiles.ContainsKey(fwName))
                                    {
                                        if (!InProgressFiles.ContainsKey(fwName))
                                        {
                                            InProgressFiles.Add(fwName, new FirmwareFile(fwPacket, _extractFolder));
                                            InProgressFileList.Add(InProgressFiles[fwName]);
                                        }
                                        else
                                        {
                                            InProgressFiles[fwName].AddPacket(fwPacket);
                                        }

                                        if (InProgressFiles[fwName].isComplete)
                                        {
                                            _completedListHasChanged = true;
                                            CompletedFiles.Add(fwName, InProgressFiles[fwName]);
                                            CompletedFileList.Add(InProgressFiles[fwName]);
                                            InProgressFileList.Remove(InProgressFiles[fwName]);
                                            InProgressFiles.Remove(fwName);
                                        }
                                    }
                                }

                                _currentFwPackets[tsPacket.packetID].Close();
                                _currentFwPackets[tsPacket.packetID] = new MemoryStream();
                            }

                            _currentFwPackets[tsPacket.packetID].Write(tsPacket.payload, 0, tsPacket.payload.Length);
                        }
                        else if (tsPacket.payload[0] == 0x00 && (tsPacket.payload[1] == 0xB5 || tsPacket.payload[1] == 0xB6))
                        {
                            _currentFwPackets.Add(tsPacket.packetID, new MemoryStream());
                            _currentFwPackets[tsPacket.packetID].Write(tsPacket.payload, 0, tsPacket.payload.Length);
                        }
                    }
                }
            }
        }

        private FwPacket ProcessNextFwPacket(BinaryReaderEndian packetReader)
        {
            FwPacket packet = new FwPacket();

            try
            {
                // Seek to next packet start
                while (true)
                {
                    if (packetReader.ReadByte() == 0x00)
                    {
                        byte tableId = packetReader.ReadByte();
                        if (tableId == 0xB5 || tableId == 0xB6)
                        {
                            packetReader.BaseStream.Seek(-2, SeekOrigin.Current);
                            break;
                        }
                        else
                        {
                            packetReader.BaseStream.Seek(-1, SeekOrigin.Current);
                        }
                    }
                }

                long startPos = packetReader.BaseStream.Position;

                // Skip over table id
                packetReader.ReadInt16();
                packet.Length = (ushort)(packetReader.ReadUInt16() & 0x0FFF);
                packet.SectionType = packetReader.ReadUInt16();
                packet.Unknown = packetReader.ReadUInt32();
                packet.VendorId = packetReader.ReadByte();
                packetReader.ReadByte();
                packet.ModelId = packetReader.ReadByte();
                packetReader.BaseStream.Seek(3, SeekOrigin.Current);
                packet.FirmwareVersion = packetReader.ReadByte();
                packet.FileOffset = packetReader.ReadUInt32();
                packet.FileLength = packetReader.ReadUInt32();

                packet.Data = packetReader.ReadBytes(packet.Length - 25);
                packet.Crc32 = packetReader.ReadUInt32();

                packetReader.BaseStream.Seek(startPos + 1, SeekOrigin.Begin);
                byte[] wholePacket = packetReader.ReadBytes(packet.Length + 3);

                packet.hasPassedCRC = (Crc32.crc32_mpeg(wholePacket, (uint)wholePacket.Length) == 0);
            }
            catch (EndOfStreamException ex)
            {
                throw ex;
            }
            catch (Exception)
            {
                packet.hasPassedCRC = false;
            }

            return packet;
        }

        private void buttonCapture_Click(object sender, RoutedEventArgs e)
        {
            _extractFolder = txtTsLocation.Text;

            if (Directory.Exists(_extractFolder))
            {
                if (Properties.Settings.Default.ExtractDirectory != _extractFolder)
                {
                    Properties.Settings.Default.ExtractDirectory = _extractFolder;
                    Properties.Settings.Default.Save();
                }

                txtTsLocation.IsEnabled = false;
                buttonBrowse.IsEnabled = false;

                // List already downloaded items
                DirectoryInfo extractDir = new DirectoryInfo(_extractFolder);

                foreach (DirectoryInfo fwFolder in extractDir.EnumerateDirectories())
                {
                    FirmwareFile fwFile = new FirmwareFile(fwFolder.FullName);

                    CompletedFiles.Add(string.Format("{0:X2}-{1:X2}-{2:X2}", fwFile.VendorId, fwFile.ModelId, fwFile.VersionId), fwFile);
                    CompletedFileList.Add(fwFile);
                }

                timer.Interval = new TimeSpan(0, 0, 1);
                timer.Tick += new EventHandler(timer_Tick);
                timer.Start();

                try
                {
                    // Start BDA Capture
                    int hr = 0;
                    IDVBSTuningSpace tuningSpace;

                    tuningSpace = (IDVBSTuningSpace)new DVBSTuningSpace();
                    hr = tuningSpace.put_UniqueName("DVBS TuningSpace");
                    hr = tuningSpace.put_FriendlyName("DVBS TuningSpace");
                    hr = tuningSpace.put__NetworkType(typeof(DVBSNetworkProvider).GUID);
                    hr = tuningSpace.put_SystemType(DVBSystemType.Satellite);
                    hr = tuningSpace.put_LowOscillator(9750000);
                    hr = tuningSpace.put_HighOscillator(10600000);

                    ITuneRequest tr = null;

                    hr = tuningSpace.CreateTuneRequest(out tr);
                    DsError.ThrowExceptionForHR(hr);

                    IDVBTuneRequest tuneRequest = (IDVBTuneRequest)tr;

                    hr = tuneRequest.put_ONID(2);
                    hr = tuneRequest.put_TSID(2004);
                    hr = tuneRequest.put_SID(4190);

                    IDVBSLocator locator = (IDVBSLocator)new DVBSLocator();
                    hr = locator.put_CarrierFrequency(11778000);
                    hr = locator.put_SymbolRate(27500000);
                    hr = locator.put_Modulation(ModulationType.ModQpsk);
                    hr = (locator as IDVBSLocator).put_SignalPolarisation(Polarisation.LinearV);
                    hr = (locator as IDVBSLocator).put_InnerFEC(FECMethod.Viterbi);
                    hr = (locator as IDVBSLocator).put_InnerFECRate(BinaryConvolutionCodeRate.Rate2_3);
                    hr = (locator as IDVBSLocator).put_OuterFEC(FECMethod.Viterbi);
                    hr = (locator as IDVBSLocator).put_OuterFECRate(BinaryConvolutionCodeRate.Rate2_3);

                    hr = tr.put_Locator(locator as ILocator);
                    Marshal.ReleaseComObject(locator);

                    this.bdaGraphBuilder = new BDAGraphBuilder();
                    this.bdaGraphBuilder.BuildGraph(tuningSpace);
                    this.bdaGraphBuilder.SubmitTuneRequest(tr);

                    // We have to do this to make it actually tune!
                    this.bdaGraphBuilder.RunGraph();
                    this.bdaGraphBuilder.StopGraph();

                    TsGrabber grabber = new TsGrabber();
                    grabber.Callback = new TsGrabber.ProcessBufferDelegate(ProcessBuffer);

                    this.bdaGraphBuilder.SetUpForTs(grabber, 1);

                    this.bdaGraphBuilder.RunGraph();

                    this.buttonCapture.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Error connecting to DVB-S:\n{0}", ex), "Connection error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            }
            else
            {
                MessageBox.Show("Cannot find extract directory specified!");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.bdaGraphBuilder != null)
            {
                this.bdaGraphBuilder.Dispose();
                this.bdaGraphBuilder = null;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            new AboutBox().ShowDialog();
        }

        private void listViewCompleted_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MenuItemOpenFolder_Click(sender, e);
        }

        private void MenuItemOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (listViewCompleted.SelectedItem != null)
            {
                FirmwareFile file = (FirmwareFile)listViewCompleted.SelectedItem;
                Process.Start(System.IO.Path.Combine(file.SaveDirectory, file.BaseName));
            }
        }

        private void MenuItemExtract_Click(object sender, RoutedEventArgs e)
        {
            if (listViewCompleted.SelectedItem != null)
            {
                FirmwareFile file = (FirmwareFile)listViewCompleted.SelectedItem;
                file.Extractor.ExtractParts(System.IO.Path.Combine(file.SaveDirectory, file.BaseName, file.FileName));
            }
        }
    }
}
