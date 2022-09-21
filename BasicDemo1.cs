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
        TCHRLibFunctionWrapper.Conn_h CHRHandle;
        TCHRDataSample[] DataSamples;
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

            DataSamples = new TCHRDataSample[Data_Length];
            OneSampleData = new double[Max_Sample_Nr*Max_Signal_Nr];
            SpecData = new short[1024];
            for (int i = 0; i < Data_Length; i++)
            {
                chart1.Series[0].Points.AddY(i);
                chart3.Series[0].Points.AddY(i);
                chart4.Series[0].Points.AddY(i);
            }
        }


        private void BtConnect_Click(object sender, EventArgs e)
        {
            bool bConnect = false;
            //connect to device
            if (sender == BtConnect)
            {
                int DeviceType = TCHRLibFunctionWrapper.CHR_Compact_Device;
                string strConInfo = TbConInfo.Text;
                //Open connection in synchronous mode
                //device buffer size has to be power of 2. When 0 is set, default buffer size 32MB is used.
                bConnect = TCHRLibFunctionWrapper.ResultSuccess( 
                    TCHRLibFunctionWrapper.OpenConnection(strConInfo, DeviceType, 
                    TCHRLibFunctionWrapper.Connection_Synchronous,  0, out CHRHandle));
                if (bConnect)
                {
                    //set up device
                    SetupDevice();
                    CurrentDataPos = 0;
                    TTimerUpdate.Enabled = true;        
                }
            }
            //close connection to device
            else
            {
                TTimerUpdate.Enabled = false;
                TCHRLibFunctionWrapper.CloseConnection(CHRHandle);
            }
            EnableGui(bConnect);

        }


        private void SetupDevice()
        {
            //default signals are: Sample counter, peak 1 value, peak 1 quality/intensity
            //signal definition for CLS device, only 16bit integer signal for peak signal
            //newer devices, float values are ordered
            SignalIDs = new int[] { 83, 256, 257 };
            //Update TextBox
            TBSODX.Text = String.Join(",", SignalIDs.Select(p => p.ToString()).ToArray());
            ScanRate = 2000;
            TBSHZ.Text = ScanRate.ToString();
            SetUpMeasuringMethod();
            SetUpScanrate();
            SetUpOutputSignals();
        }

        private void SetUpMeasuringMethod()
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
                if (!TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.ExecCommand(CHRHandle, hCmd, out hRsp)))
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

        private void SetUpOutputSignals()
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
                if (!TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.ExecCommand(CHRHandle, hCmd, out hRsp)))
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


        private void SetUpScanrate()
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
                if (!TCHRLibFunctionWrapper.ResultSuccess(TCHRLibFunctionWrapper.ExecCommand(CHRHandle, hCmd, out hRsp)))
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
                            DataSamples[CurrentDataPos].Distance = 0;
                            DataSamples[CurrentDataPos].Intensity = 0;
                            DataSamples[CurrentDataPos].SampleCounter = 0;
                            if (SigNumber > 0)
                            {
                                //since we order sample counter as the first signal
                                //read in the sample counter
                                if (SigNumber > 0)
                                {
                                    DataSamples[CurrentDataPos].SampleCounter = (int)GetSignalData(i, SigNumber, 0);
                                    if (((DataSamples[CurrentDataPos].SampleCounter < LastCounter)) && (DataSamples[CurrentDataPos].SampleCounter != 0))
                                    {
                                        Console.WriteLine("Error in counter");
                                    }
                                    LastCounter = DataSamples[CurrentDataPos].SampleCounter;

                                }
                                //read in distance data (for the first channel in case of multi-channel device)
                                if (SigNumber > 1)
                                    DataSamples[CurrentDataPos].Distance = GetSignalData(i, SigNumber, 1);
                                //read in intensity data (for the first channel in case of multi-channel device)
                                if (SigNumber > 2)
                                    DataSamples[CurrentDataPos].Intensity = GetSignalData(i, SigNumber, 2);
                                
                                
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
                        chart3.Series[0].Points[i].YValues[0] = DataSamples[i].Distance;
                    if (SigNumber > 2)
                        chart4.Series[0].Points[i].YValues[0] = DataSamples[i].Intensity;
                    
                }

                chart3.ChartAreas[0].RecalculateAxesScale();
                chart3.Invalidate();
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
            TTimerUpdate.Enabled = false;
            BtAutoSaveData.Enabled = false;
            BtSend.Enabled = false;
            TBCMD.Enabled = false;
            
            double[] tempDataBuffer = new double[0];
            Int64 BufSize = 0;
            //check minimum required buffer size
            TCHRLibFunctionWrapper.ActivateAutoBufferMode(CHRHandle, IntPtr.Zero, 1000, ref BufSize);
            tempDataBuffer = new double[BufSize/sizeof(double)];
            GCHandle pinnedArray = GCHandle.Alloc(tempDataBuffer, GCHandleType.Pinned);
            IntPtr unmanagedPointer = pinnedArray.AddrOfPinnedObject();
            int nSigNr = (int)(BufSize / 1000 / sizeof(double));
            //begin automatic data save to buffer
            if (TCHRLibFunctionWrapper.ResultSuccess(
                TCHRLibFunctionWrapper.ActivateAutoBufferMode(CHRHandle, unmanagedPointer, 1000, ref BufSize)))
            {
                //check whether buffer save is finished
                while (TCHRLibFunctionWrapper.GetAutoBufferStatus(CHRHandle) == TCHRLibFunctionWrapper.Auto_Buffer_Saving)
                    System.Threading.Thread.Sleep(20); ;
                //upon finish, write data to temp.txt file
                using (StreamWriter sw = new StreamWriter("temp.txt"))
                {
                    sw.WriteLine("Automatically saved 1000 samples:");
                    for (int i=0;i<1000;i++)
                    {
                        for (int j=0; j<nSigNr; j++)           
                            sw.Write("{0:0.0}; ", tempDataBuffer[i*nSigNr+j]);
                        sw.WriteLine("");
                    }
                }
                TTimerUpdate.Enabled = true;
                BtAutoSaveData.Enabled = true;
                BtSend.Enabled = true;
                TBCMD.Enabled = true;
            }
            pinnedArray.Free();
        }

        private void RBConfocal_Click(object sender, EventArgs e)
        {
            SetUpMeasuringMethod();
        }

        private void TBSHZ_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
                SetUpScanrate();
        }

        private void TBSODX_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
                SetUpOutputSignals();
        }

    }
}
