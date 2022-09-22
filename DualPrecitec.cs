/*
This demo shows how to use ChrocodileLib to perform basic synchronous communication with different types of the devices.
It includes sending commands, reading response, use "GetNextSamples" and "ActivateAutoBufferMode" to read data.
The demo utilises the basic function provided by ChrocodileLib
*/


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace TCHRLibBasicDemo2
{
    public partial class TBasicDemo1 : Form
    {


        struct TCHRDataSample
        {
            public double Distance;
            public double Intensity;
            public Int32 SampleCounter;
        }

        const int Data_Length = 1024;
        const int Max_Signal_Nr = 2048*16;
        const int Max_Sample_Nr = 1024;
        const string ConnectionError = "Not Connected";
        TCHRLibFunctionWrapper.Conn_h CHRHandle;
        TCHRLibFunctionWrapper.Conn_h CHRHandle2;
        bool connected = false;
        (TCHRDataSample, TCHRDataSample)[] DataSamples;
        double[] OneSampleData;
        short[] SpecData;
        int CurrentDataPos;
        int LastCounter = 0;

        int MeasuringMethod = TCHRLibFunctionWrapper.Confocal_Measurement;
        int[] SignalIDs;
        float ScanRate;


        public TBasicDemo1()
        {
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            var rnd = new Random();

            DataSamples = new (TCHRDataSample, TCHRDataSample)[Data_Length];
            OneSampleData = new double[Max_Sample_Nr*Max_Signal_Nr];
            SpecData = new short[1024];
            for (int i = 0; i < Data_Length; i++)
            {
                
                chart1.Series[0].Points.AddY(i);
                distChart.Series[0].Points.AddY(i);
                var rd = rnd.NextDouble() * i;
                distChart.Series[1].Points.AddY(rd);
                chart4.Series[0].Points.AddY(i);
            }
        }


        private void BtConnect_Click(object sender, EventArgs e)
        {
            //connect to device
            if (sender == BtConnect)
            {
                string strConInfo = TbConInfo.Text;
                string strConInfo2 = TbConInfo2.Text;
                //Open connection in synchronous mode
                //device buffer size has to be power of 2. When 0 is set, default buffer size 32MB is used.
                connected = ConnectDevice(strConInfo, out CHRHandle);
                connected &= ConnectDevice(strConInfo2, out CHRHandle2);
                
            }
            //close connection to device
            else
            {
                TTimerUpdate.Enabled = false;
                TCHRLibFunctionWrapper.CloseConnection(CHRHandle);
                TCHRLibFunctionWrapper.CloseConnection(CHRHandle2);
            }
            EnableGui(connected);

        }

        private bool ConnectDevice(string connStr, out TCHRLibFunctionWrapper.Conn_h connHandle)
        {
            bool bConnect = false;
            int DeviceType = TCHRLibFunctionWrapper.CHR_Compact_Device;
            bConnect = TCHRLibFunctionWrapper.ResultSuccess(
                TCHRLibFunctionWrapper.OpenConnection(connStr, DeviceType,
                TCHRLibFunctionWrapper.Connection_Synchronous, 0, out connHandle));
            if (bConnect)
            {
                //set up device
                SetupDevice(connHandle);
                CurrentDataPos = 0;
                TTimerUpdate.Enabled = true;
            }
            else
            {
                string errMsg = String.Format("Could not connect {0}", connStr);
                _ = MessageBox.Show(errMsg, ConnectionError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return bConnect;
        }



        private void SetupDevice(TCHRLibFunctionWrapper.Conn_h handle)
        {
            //default signals are: Sample counter, peak 1 value, peak 1 quality/intensity
            //signal definition for CLS device, only 16bit integer signal for peak signal
            //newer devices, float values are ordered
            SignalIDs = new int[] { 83, 256, 257 };
            //Update TextBox
            TBSODX.Text = String.Join(",", SignalIDs.Select(p => p.ToString()).ToArray());
            ScanRate = 2000;
            TBSHZ.Text = ScanRate.ToString();
            SetUpMeasuringMethod(handle);
            SetUpScanrate(handle);
            SetUpOutputSignals(handle);
        }

        private void SetUpMeasuringMethod(TCHRLibFunctionWrapper.Conn_h handle)
        {
            try
            {
                int nMMD = TCHRLibFunctionWrapper.Confocal_Measurement;
                TCHRLibFunctionWrapper.Cmd_h hCmd;
                TCHRLibFunctionWrapper.Rsp_h hRsp;
                //Create measuring method command
                TCHRLibFunctionWrapper.NewCommand(TCHRLibFunctionWrapper.CmdID_Measuring_Method, 0, out hCmd);
                //Add measuring method argument
                TCHRLibFunctionWrapper.AddCommandIntArg(hCmd, nMMD);
                //Execute command and check result
                if (!TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.ExecCommand(handle, hCmd, out hRsp)))
                    Debug.Fail("Cannot set measuring method");
                else
                {
                    //Get response measuring method argument
                    TCHRLibFunctionWrapper.GetResponseIntArg(hRsp, 0, out MeasuringMethod);
                }
            }
            catch
            {              
            }
        }

        private void SetUpOutputSignals(TCHRLibFunctionWrapper.Conn_h handle)
        {
            try
            {
                //Set device output signals
                //Sample counter, peak 1 value, peak 1 quality/intensity
                char[] delimiters = new char[] { ' ', ',', ';' };
                int[] signals = TBSODX.Text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).
                    Select(int.Parse).ToArray();
                TCHRLibFunctionWrapper.Cmd_h hCmd;
                TCHRLibFunctionWrapper.Rsp_h hRsp;
                //Create output signal command
                TCHRLibFunctionWrapper.NewCommand(TCHRLibFunctionWrapper.CmdID_Output_Signals, 0, out hCmd);
                //Add output signal argument
                TCHRLibFunctionWrapper.AddCommandIntArrayArg(hCmd, signals, signals.Length);
                //Execute command and check result
                if (!TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.ExecCommand(handle, hCmd, out hRsp)))
                    Debug.Fail("Cannot set output signals");
                else
                {
                    //Get response output signal argument
                    ReadOutputSignalResponse(hRsp);
                }
            }
            catch
            {
                
            }
            TBSODX.Text = String.Join(",", SignalIDs.Select(p => p.ToString()).ToArray());
        }


        private void ReadOutputSignalResponse(TCHRLibFunctionWrapper.Rsp_h _hRsp)
        {
            IntPtr pSig;
            Int32 nLength;
            TCHRLibFunctionWrapper.GetResponseIntArrayArg(_hRsp, 0, out pSig, out nLength);
            SignalIDs = new Int32[nLength];
            if (nLength > 0)
                Marshal.Copy(pSig, SignalIDs, 0, nLength);
        }


        private void SetUpScanrate(TCHRLibFunctionWrapper.Conn_h handle)
        {
            try
            {
                float nSHZ = float.Parse(TBSHZ.Text); 
                TCHRLibFunctionWrapper.Cmd_h hCmd;
                TCHRLibFunctionWrapper.Rsp_h hRsp;
                //Create scan rate command
                TCHRLibFunctionWrapper.NewCommand(TCHRLibFunctionWrapper.CmdID_Scan_Rate, 0, out hCmd);
                //Add scan rate argument
                TCHRLibFunctionWrapper.AddCommandFloatArg(hCmd, nSHZ);
                //Execute command and check result
                if (!TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.ExecCommand(handle, hCmd, out hRsp)))
                    Debug.Fail("Cannot set measuring method");
                //Get response scan rate argument
                else
                    TCHRLibFunctionWrapper.GetResponseFloatArg(hRsp, 0, out ScanRate);
            }
            catch
            {
                
            }
            TBSHZ.Text = ScanRate.ToString();
        }


        private void EnableGui(bool _bEnabled)
        {
            BtConnect.Enabled = !_bEnabled;
            BtDisCon.Enabled = _bEnabled;
            BtSend.Enabled = _bEnabled;
            TBCMD.Enabled = _bEnabled;
            BtAutoSaveData.Enabled = _bEnabled;
            TBSHZ.Enabled = _bEnabled;
            TBSODX.Enabled = _bEnabled;
        }


        private void TTimerUpdate_Tick(object sender, EventArgs e)
        {
            
            int SigNumber = 0;
            
            //read in CHRocodile data with GetNextSamples
            while (true)
            {
                Int64 nCount = 1000;
                Int64 nSize;
                TCHRLibFunctionWrapper.TSampleSignalGeneralInfo sGenInfo;
                IntPtr pInfo=IntPtr.Zero;
                IntPtr pData = IntPtr.Zero;
                //try to read 1000 samples without waiting, 1000 is an arbitary number
                var nRes = TCHRLibFunctionWrapper.GetNextSamples(CHRHandle, ref nCount, out pData, out nSize, out sGenInfo, out pInfo);
                IntPtr pData2 = IntPtr.Zero;
                var nRes2 = TCHRLibFunctionWrapper.GetNextSamples(CHRHandle2, ref nCount, out pData2, out nSize, out sGenInfo, out pInfo);
                if (TCHRLibFunctionWrapper.ResultSuccess(nRes))
                {
                    if (nCount > 0)
                    {
                        //overall signal number
                        SigNumber = sGenInfo.GlobalSignalCount + sGenInfo.PeakSignalCount * sGenInfo.ChannelCount;

                        if (SigNumber > Max_Signal_Nr)
                            SigNumber = Max_Signal_Nr;
                        //Copy over data
                        Marshal.Copy(pData, OneSampleData, 0, SigNumber * (Int32)nCount);
                        for (int i = 0; i < nCount; i++)
                        {
                            DataSamples[CurrentDataPos] = default((TCHRDataSample, TCHRDataSample));
                        if (SigNumber > 0)
                            {
                                DataSamples[CurrentDataPos].Item1.SampleCounter = (int)GetSignalData(i, SigNumber, 0);
                                if (((DataSamples[CurrentDataPos].Item1.SampleCounter < LastCounter)) && (DataSamples[CurrentDataPos].Item1.SampleCounter != 0))
                                {
                                    Console.WriteLine("Error in counter");
                                }
                                LastCounter = DataSamples[CurrentDataPos].Item1.SampleCounter;

                                //read in distance data (for the first channel in case of multi-channel device)
                                if (SigNumber > 1)
                                    DataSamples[CurrentDataPos].Item1.Distance = GetSignalData(i, SigNumber, 1);
                                //read in intensity data (for the first channel in case of multi-channel device)
                                if (SigNumber > 2)
                                    DataSamples[CurrentDataPos].Item1.Intensity = GetSignalData(i, SigNumber, 2);
                                
                                
                                CurrentDataPos++;
                                if (CurrentDataPos >= Data_Length)
                                    CurrentDataPos = 0;
                            }
                        }
                    }
                    //no data available any more, break 
                    if (nRes == TCHRLibFunctionWrapper.Read_Data_Not_Enough)
                        break;
                }
                else
                {
                    TCHRLibFunctionWrapper.FlushConnectionBuffer(CHRHandle);
                    break;
                }
            }
            //refresh data display
            if ((TCData.SelectedTab == TPData) && (SigNumber>0))
            {
                for (int i = 0; i < Data_Length; i++)
                {
                    if (SigNumber > 1)
                        distChart.Series[0].Points[i].YValues[0] = DataSamples[i].Item1.Distance;
                        distChart.Series[1].Points[i].YValues[0] = DataSamples[i].Item2.Distance;
                    if (SigNumber > 2)
                        chart4.Series[0].Points[i].YValues[0] = DataSamples[i].Item1.Intensity;
                    
                }

                distChart.ChartAreas[0].RecalculateAxesScale();
                distChart.Invalidate();
                chart4.ChartAreas[0].RecalculateAxesScale();
                chart4.Invalidate();
            }

            //download spectrum
            if (TCData.SelectedTab == TPSpec)
            {
                int SpecType = TCHRLibFunctionWrapper.Spectrum_Raw;
                if (RBConfocalSpec.Checked)
                    SpecType = TCHRLibFunctionWrapper.Spectrum_Confocal;
                else if (RBFFTSpec.Checked)
                    SpecType = TCHRLibFunctionWrapper.Spectrum_FT;
                //Create spectrum downloading command
                TCHRLibFunctionWrapper.Cmd_h hCmd;
                TCHRLibFunctionWrapper.Rsp_h hRsp;
                TCHRLibFunctionWrapper.NewCommand(TCHRLibFunctionWrapper.CmdID_Download_Spectrum, 0, out hCmd);
                //for downloading spectra of several channles from multi-channel device, still needs to add start channel index and channel count
                TCHRLibFunctionWrapper.AddCommandIntArg(hCmd, SpecType);
                //Execute command and check the result
                if (TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.ExecCommand(CHRHandle, hCmd, out hRsp)))
                {
                    IntPtr pSpec = IntPtr.Zero;
                    Int32 nLength;
                    //Get the spectrum data
                    // the fourth argument ist spectrum data, first arguement: spectrum type; second: start channel index; third: channel count
                    if (TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.GetResponseBlobArg(hRsp, 3, out pSpec, out nLength)))
                    {
                        Marshal.Copy(pSpec, SpecData, 0, nLength / 2);
                        for (int i = 0; i < nLength / 2; i++)
                            chart1.Series[0].Points[i].YValues[0] = SpecData[i];
                        for (int i = nLength / 2; i < 1024; i++)
                            chart1.Series[0].Points[i].YValues[0] = 0;
                        chart1.ChartAreas[0].RecalculateAxesScale();
                        chart1.Invalidate();
                    }
                }
            }
        }

        private double GetSignalData(int _nSampleIndex, int _nSignalNr, int _nSignalIndex)
        {
            double data = OneSampleData[_nSampleIndex * _nSignalNr + _nSignalIndex];
            if (Double.IsNaN(data))
                data = 0;
            return (data);
        }

        //Execute string command
        private void BtSend_Click(object sender, EventArgs e)
        {
            string strCmd = TBCMD.Text;
            TCHRLibFunctionWrapper.Cmd_h hCmd;
            TCHRLibFunctionWrapper.Rsp_h hRsp;
            //create command from string
            var nRes = TCHRLibFunctionWrapper.NewCommandFromString(strCmd, out hCmd);
            if (TCHRLibFunctionWrapper.ResultSuccess(nRes))
            {
                //Execute command and check result
                if (!TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.ExecCommand(CHRHandle, hCmd, out hRsp)))
                    Debug.Fail("Cannot execute string command");                
                else
                {
                    try
                    {
                        //Get Response string
                        Int64 nLength = 0;
                        TCHRLibFunctionWrapper.ResponseToString(hRsp, null, ref nLength);
                        var strRsp = new StringBuilder((int)nLength);
                        nRes = TCHRLibFunctionWrapper.ResponseToString(hRsp, strRsp, ref nLength);
                        if (RTResponse.Text != "")
                            RTResponse.AppendText(Environment.NewLine);
                        RTResponse.AppendText(strRsp.ToString());

                        //Update corresponding parameter text box based on the response
                        TCHRLibFunctionWrapper.TResponseInfo sInfo;
                        TCHRLibFunctionWrapper.GetResponseInfo(hRsp, out sInfo);
                        switch (sInfo.CmdID)
                        {
                            //In case of measuring method response
                            case (TCHRLibFunctionWrapper.CmdID_Measuring_Method):
                                TCHRLibFunctionWrapper.GetResponseIntArg(hRsp, 0, out MeasuringMethod);
                                break;
                            //In case of scan rate response
                            case (TCHRLibFunctionWrapper.CmdID_Scan_Rate):
                                TCHRLibFunctionWrapper.GetResponseFloatArg(hRsp, 0, out ScanRate);
                                TBSHZ.Text = ScanRate.ToString();
                                break;
                            //In case of output signal response
                            case (TCHRLibFunctionWrapper.CmdID_Output_Signals):
                                ReadOutputSignalResponse(hRsp);
                                TBSODX.Text = String.Join(",", SignalIDs.Select(p => p.ToString()).ToArray());
                                break;
                            default:
                                break;
                        }
                    }
                    catch
                    {

                    }
                    
                }
            }
        }

        private void TBCMD_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                BtSend_Click(TBCMD, e);
            }
        }

        private void BtAutoSaveData_Click(object sender, EventArgs e)
        {
            EnableGui(false);

            int numSamples = 1000;


            double[] tempDataBuffer1 = new double[0];
            double[] tempDataBuffer2 = new double[0];

            Int64 BufSize = 0;
            //check minimum required buffer size
            if (connected)
            {
                TCHRLibFunctionWrapper.ActivateAutoBufferMode(CHRHandle, IntPtr.Zero, numSamples, ref BufSize);
                TCHRLibFunctionWrapper.ActivateAutoBufferMode(CHRHandle2, IntPtr.Zero, numSamples, ref BufSize);
            }
            else
            {
                string errMsg = "Could not ActivateAutoBufferMode";
                _ = MessageBox.Show(errMsg, ConnectionError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            tempDataBuffer1 = new double[BufSize / sizeof(double)];
            tempDataBuffer2 = new double[BufSize / sizeof(double)];
            GCHandle pinnedArray1 = GCHandle.Alloc(tempDataBuffer1, GCHandleType.Pinned);
            GCHandle pinnedArray2 = GCHandle.Alloc(tempDataBuffer1, GCHandleType.Pinned);
            IntPtr unmanagedPointer1 = pinnedArray1.AddrOfPinnedObject();
            IntPtr unmanagedPointer2 = pinnedArray2.AddrOfPinnedObject();
            int nSigNr = (int)(BufSize / numSamples / sizeof(double));
            //begin automatic data save to buffer
            if (TCHRLibFunctionWrapper.ResultSuccess(
                    TCHRLibFunctionWrapper.ActivateAutoBufferMode(CHRHandle, unmanagedPointer1, numSamples, ref BufSize)) &&
                TCHRLibFunctionWrapper.ResultSuccess(
                    TCHRLibFunctionWrapper.ActivateAutoBufferMode(CHRHandle2, unmanagedPointer2, numSamples, ref BufSize)))
            {
                while(true)
                {
                    //check whether buffer save is finished
                    var preci1_state = TCHRLibFunctionWrapper.GetAutoBufferStatus(CHRHandle);
                    var preci2_state = TCHRLibFunctionWrapper.GetAutoBufferStatus(CHRHandle2);
                    if (preci1_state == TCHRLibFunctionWrapper.Auto_Buffer_Saving || preci2_state == TCHRLibFunctionWrapper.Auto_Buffer_Saving)
                    {
                        System.Threading.Thread.Sleep(20);
                    }
                    else
                    {
                        break; 
                    }
                }
                //upon finish, write data to temp.txt file
                using (StreamWriter sw = new StreamWriter("temp.txt"))
                {
                    sw.WriteLine(String.Format("Automatically saved {0} samples:", numSamples));
                    for (int i=0;i< numSamples; i++)
                    {
                        for (int j = 0; j < nSigNr; j++)
                            sw.Write("{0:0.0}; ", tempDataBuffer1[i * nSigNr + j]);
                        for (int j = 0; j < nSigNr; j++)
                            sw.Write("{0:0.0}; ", tempDataBuffer2[i * nSigNr + j]);
                        sw.WriteLine("");
                    }
                }
                TTimerUpdate.Enabled = true;
                BtAutoSaveData.Enabled = true;
                BtSend.Enabled = true;
                TBCMD.Enabled = true;
            }
            pinnedArray1.Free();
            pinnedArray2.Free();
        }

        private void TBSHZ_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
                SetUpScanrate(CHRHandle);
                SetUpScanrate(CHRHandle2);
        }

        private void TBSODX_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
                SetUpOutputSignals(CHRHandle);
                SetUpOutputSignals(CHRHandle2);
        }

    }
}
