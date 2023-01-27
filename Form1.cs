﻿using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Numerics;
using System.Collections;
using ScottPlot;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.Drawing;
using System;
using Timers = System.Timers;
//using ScottPlot.Plottable;
using System.Reflection;
using System.Windows.Forms.DataVisualization.Charting;

namespace plotBrembs
{
    public partial class Form1 : Form
    {
        private static Mutex mut = new Mutex();
        private static System.Threading.Timer simulationTimer = null;

        Timers.Timer aTimer = null;

        static int _serialClear = 0;

        DateTime beginTime;

        public SerialPort _serialPort = null;
        public Thread ReadSerialDataThread;

        private double[] liveDataAD = new double[1080];
        private double[] liveDataPIX = new double[1080];

        private List<byte> list = new List<byte>();
        private int nextValueIndex = 0;

        //SignalPlot signalPlotAD = null;
        //SignalPlot signalPlotPIX = null;

        public delegate void ShowSerialData(List<byte> _readSerialValue);

        Version version = new Version();

        public Form1()
        {
            InitializeComponent();

            version = Assembly.GetExecutingAssembly().GetName().Version;

            

            List<string> portList = new List<string>();

            portList.AddRange(SerialPort.GetPortNames());
            portList.Sort();

            string[] portNames = portList.ToArray();

            if (portNames.Length > 0)
            {
                serialComboBox.Items.AddRange(portNames);
                serialComboBox.SelectedIndex = 0;
            }


            this.Text = version.ToString();

            _serialPort = new SerialPort();
            _serialPort.BaudRate = 115200;
            _serialPort.Parity = Parity.None;

            //signalPlotAD = formsPlot1.Plot.AddSignal(liveDataAD);
            //signalPlotPIX = formsPlot1.Plot.AddSignal(liveDataPIX);

            //formsPlot1.Plot.Benchmark(enable: true);


            //signalPlotAD.YAxisIndex = 0;
            //signalPlotPIX.YAxisIndex = 1;

            //formsPlot1.Plot.SetAxisLimitsX(xMin: 0, xMax: liveDataAD.Length);
            //formsPlot1.Plot.SetAxisLimits(xMin: 0, xMax: 4000, yMin: -1, yMax: 1, yAxisIndex: 0);
            //formsPlot1.Plot.SetAxisLimits(xMin: 0, xMax: liveDataAD.Length, yMin: -1, yMax: 800, yAxisIndex: 1);

            ////formsPlot1.Plot.YAxis.LockLimits(true);
            ////formsPlot1.Plot.XAxis.LockLimits(true);
            ////formsPlot1.Plot.YAxis2.LockLimits(true);

            //formsPlot1.Plot.Title(Application.ProductName);
            //formsPlot1.Plot.Grid(true);

            //signalPlotAD.Color = Color.Magenta;
            //signalPlotPIX.Color = Color.Green;
            //signalPlotAD.LineWidth = 2;
            //signalPlotPIX.LineWidth = 2;
            //signalPlotAD.YAxisIndex = 0;
            //signalPlotPIX.YAxisIndex = 1;

            //formsPlot1.Plot.YAxis.Color(Color.Magenta);
            //formsPlot1.Plot.YAxis.Label("AD-Value");
            //formsPlot1.Plot.YAxis2.Color(Color.Green);
            //formsPlot1.Plot.YAxis2.Label("PIXEL-Value");
            //formsPlot1.Plot.XAxis.Label("Time");

            //formsPlot1.Plot.YAxis2.Ticks(true);

            //formsPlot1.Configuration.RightClickDragZoom = false;
            //formsPlot1.Configuration.ScrollWheelZoom = false;

            //formsPlot1.Render();

            SetTimer();

            beginTime = DateTime.Now;

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //double[] dataX = new double[] { 1, 2, 3, 4, 5 };
            //double[] dataY = new double[] { 1, 4, 9, 16, 25 };
            //formsPlot1.Plot.AddScatter(dataX, dataY);
            //formsPlot1.Refresh();
            ChartArea ChartArea0 = new ChartArea("liveData");
            chart1.ChartAreas.Add(ChartArea0);

            chart1.Series.Add("liveDataAD");
            chart1.Series.Add("liveDataPIX");

            //Ausssehen festlegen
            chart1.Series["liveDataAD"].ChartType = SeriesChartType.Line;
            chart1.Series["liveDataPIX"].ChartType = SeriesChartType.Line;

            //Start value
            chart1.Series["liveDataAD"].Points.DataBindY(liveDataAD);
            chart1.Series["liveDataPIX"].Points.DataBindY(liveDataPIX);

            chart1.Series[0].Color = Color.Green;
            chart1.Series[1].Color = Color.Red;

            chart1.Series[0].YAxisType = AxisType.Primary;
            chart1.Series[0].YAxisType = AxisType.Secondary;

            chart1.ChartAreas[0].AxisY.Minimum = 0;
            chart1.ChartAreas[0].AxisY.Maximum = 800;
            chart1.ChartAreas[0].AxisY.MajorGrid.Enabled = true;

            chart1.ChartAreas[0].AxisY2.Minimum = -0.6;
            chart1.ChartAreas[0].AxisY2.Maximum = 0.6;
            chart1.ChartAreas[0].AxisY2.MajorGrid.Enabled = true;
            chart1.ChartAreas[0].AxisY2.Enabled = AxisEnabled.True;

            Debug.WriteLine("Plot load");
        }

        private void startSerial_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.PortName = serialComboBox.Text;
                    _serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialDataReceivedEventHandler);
                    _serialPort.Open();
                    if (_serialPort.BytesToRead == 0)
                    {
                        _serialPort.WriteLine("Stop");
                        _serialPort.WriteLine("Start");
                    }
                    else
                    {
                        _serialPort.WriteLine("Start");
                    }
                    aTimer.Start();

                }
                else
                {
                    _serialPort.WriteLine("Stop");
                    _serialPort.Close();
                    aTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Serial failed", "Opening SerialPort Event");
            }



        }

        private void simulateData_Click(object sender, EventArgs e)
        {
            TimerCallback timerCallback = new TimerCallback(sendSimulationData);
            simulationTimer = new System.Threading.Timer(timerCallback, null, 0, 50);


            Debug.WriteLine("Press the Enter key to exit the program at any time... ");
        }

        private void sendSimulationData(Object source)
        {
            Debug.WriteLine("The Elapsed event was raised at {0}", DateTime.Now);
            mut.WaitOne();
            //list.Add(Convert.ToByte(0xFF));
            //list.Add(Convert.ToByte(0x07));
            list.Add(Convert.ToByte(0x00));
            list.Add(Convert.ToByte(0x08));
            list.Add(Convert.ToByte(0xC8));
            list.Add(Convert.ToByte(0x00));
            list.Add(Convert.ToByte(0x0A));
            mut.ReleaseMutex();
            Debug.WriteLine("Read");
            this.BeginInvoke(new ShowSerialData(LineReceived), list);

        }

        private void SerialDataReceivedEventHandler(object sender, SerialDataReceivedEventArgs e)
        {
            {
                try
                {
                    SerialPort sp = (SerialPort)sender;
                    while (sp.BytesToRead > 0)
                    {
                        mut.WaitOne();
                        list.Add(Convert.ToByte(sp.ReadByte()));
                        mut.ReleaseMutex();
                    }
                }
                catch
                {

                }
                this.BeginInvoke(new ShowSerialData(LineReceived), new object[] { list });
            }
        }

        private void LineReceived(List<byte> _serialValue)
        {
            ushort nextValueUint = 0;
            short nextValueAD = 0;
            ushort nextValuePIX = 0;
            byte[] byteArray = new byte[4];

            // while (_serialValue.Count >= 5 && _serialValue.IndexOf(0x0A) == 4)
            try
            {
                while (_serialValue.Count >= 5)
                {
                    if (_serialValue.IndexOf(0x0A) != 4)
                    {
                        throw new Exception("LF not found");
                    }
                    //Debug.WriteLine(_serialValue.IndexOf(0x0A));
                    byteArray = _serialValue.GetRange(0, 4).ToArray();
                    nextValueUint = BitConverter.ToUInt16(byteArray, 0);
                    BitArray nextValueADBit = new BitArray(byteArray);
                    if (nextValueADBit[11] == true)
                    {
                        nextValueADBit = nextValueADBit.Or(new BitArray(System.BitConverter.GetBytes(0xF000)));
                    }
                    nextValueADBit.CopyTo(byteArray, 0);
                    nextValueAD = BitConverter.ToInt16(byteArray, 0);
                    nextValuePIX = BitConverter.ToUInt16(byteArray, 2);

                    Debug.WriteLine(nextValueAD);
                    //Debug.WriteLine(nextValuePIX);

                    //TimeSpan elapsedTime = new TimeSpan(DateTime.Now.Ticks - beginTime.Ticks);

                    //debugTextbox.Text = elapsedTime.Milliseconds.ToString() + "/ " + nextValueAD.ToString() + "/ " + nextValuePIX.ToString();

                    mut.WaitOne();
                    _serialValue.RemoveRange(0, 5);
                    //Debug.WriteLine(_serialValue.Count);
                    mut.ReleaseMutex();

                    nextValueIndex = (nextValueIndex < liveDataAD.Length - 1) ? nextValueIndex + 1 : 0;

                    //Calculation AD-Value
                    liveDataAD[nextValueIndex] = Convert.ToDouble(nextValueAD * 244.14 * Math.Pow(10, -6));
                    liveDataPIX[nextValueIndex] = Convert.ToDouble(nextValuePIX);

                    //beginTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                _serialClear++;
                Debug.WriteLine(_serialClear);
                _serialValue.Clear();
            }


            //Debug.WriteLine(_serialValue.Count);
            updateData();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process procces = System.Diagnostics.Process.GetCurrentProcess();
            System.Diagnostics.ProcessThreadCollection threadCollection = procces.Threads;

            string threads = string.Empty;

            foreach (System.Diagnostics.ProcessThread proccessThread in threadCollection)
            {
                threads += string.Format("Thread Id: {0}, ThreadState: {1}\r\n", proccessThread.Id, proccessThread.ThreadState);
            }

            Debug.WriteLine(Thread.CurrentThread.ManagedThreadId);


            MessageBox.Show(threads);
        }

        private void SetTimer()
        {
            aTimer = new Timers.Timer(250);
            aTimer.Elapsed += this.OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.SynchronizingObject = this;
            aTimer.Enabled = false;
        }

        private void updateData()
        {
            //formsPlot1.Refresh();

            chart1.Series["liveDataAD"].Points.RemoveAt(nextValueIndex);
            chart1.Series["liveDataPIX"].Points.RemoveAt(nextValueIndex);
            chart1.Series["liveDataAD"].Points.InsertY(nextValueIndex, liveDataAD[nextValueIndex]);
            chart1.Series["liveDataPIX"].Points.InsertY(nextValueIndex, liveDataPIX[nextValueIndex]);

            Debug.WriteLine(liveDataAD[nextValueIndex].ToString() + " " + liveDataPIX[nextValueIndex].ToString());
        }

        private void OnTimedEvent(Object source, Timers.ElapsedEventArgs e)
        {
#if DEBUG
            Stopwatch timer = new Stopwatch();
            timer.Start();
#endif
            //formsPlot1.Plot.AxisAutoY(0.1, 0);
            //formsPlot1.Plot.AxisAutoY(0.1, 1);
            //Debug.WriteLine(formsPlot1.Plot.GetAxisLimits().ToString());
            //formsPlot1.Refresh();
            TimeSpan elapsedTime = new TimeSpan(DateTime.Now.Ticks - beginTime.Ticks);
            //debugTextbox.Text = elapsedTime.Milliseconds.ToString() + " " + nextValueIndex.ToString() + " " + _serialClear.ToString();
            beginTime = DateTime.Now;
#if DEBUG
            timer.Stop();
            Debug.WriteLine("Render Taken: " + timer.Elapsed.TotalMilliseconds.ToString("#,##0.00 'milliseconds'"));
#endif

        }

        private void Form1_FormClosing(Object sender, FormClosingEventArgs e)
        {

            //System.Text.StringBuilder messageBoxCS = new System.Text.StringBuilder();
            //messageBoxCS.AppendFormat("{0} = {1}", "CloseReason", e.CloseReason);
            //messageBoxCS.AppendLine();
            //messageBoxCS.AppendFormat("{0} = {1}", "Cancel", e.Cancel);
            //messageBoxCS.AppendLine();
            //MessageBox.Show(messageBoxCS.ToString(), "FormClosing Event");

            try
            {
                if (_serialPort.IsOpen)
                {
                    Thread.Sleep(100);
                    if (_serialPort.BytesToRead > 0)
                    {
                        _serialPort.WriteLine("Stop");
                        _serialPort.Close();
                    }
                }
                else
                {

                }

                if (aTimer.Enabled == true)
                {
                    aTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "SerialInterface Event");
            }
        }

    }
}