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
using System.Reflection;
using System.Windows.Forms.DataVisualization.Charting;

namespace plotBrembs
{

    public partial class Form1 : Form
    {
        private static Mutex mut = new Mutex();
        private static System.Threading.Timer simulationTimer = null;

        private serialInterface serialCom;
        private writeFile fileWriter;

        public Thread ReadSerialDataThread;

        private double[] liveDataAD = new double[1080];
        private double[] liveDataPIX = new double[1080];

        private List<byte> list = new List<byte>();
        public int nextValueIndex = 0;

        public delegate void ShowSerialData();

        Version version = new Version();

        ColorPattern patternColor;

        DisplayPattern patternDisplay;

        RotationValue valueRotation;

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

            serialCom = new serialInterface();
            serialCom.OnDataReceived += SerialInterface_OnDataReceived;

            fileWriter = new writeFile();


        }

        private void Form1_Load(object sender, EventArgs e)
        {
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
            int returnValue = 0;
            if (serialCom != null)
            {
                returnValue = serialCom.openPort(serialComboBox.Text);                
                switch (returnValue)
                {
                    case 0:
                        serialOpen.Image = Properties.Resources._269210;
                        break;
                    case 1:
                        serialOpen.Image = Properties.Resources._269251;
                        startSerial.Text = "Stop";
                        break;
                    case 2:
                        serialOpen.Image = Properties.Resources._269251;
                        startSerial.Text = "Stop";
                        break;
                }
            }
           
        }

        private void simulateData_Click(object sender, EventArgs e)
        {
            TimerCallback timerCallback = new TimerCallback(sendSimulationData);
            simulationTimer = new System.Threading.Timer(timerCallback, null, 0, 500);


            Debug.WriteLine("Press the Enter key to exit the program at any time... ");
        }

        private void sendSimulationData(Object source)
        {
            list.Add(Convert.ToByte(0xC9));
            list.Add(Convert.ToByte(0x00));
            list.Add(Convert.ToByte(0xC8));
            list.Add(Convert.ToByte(0x00));
            list.Add(Convert.ToByte(0x0A));

            this.BeginInvoke(new ShowSerialData(convertList));

        }

        private void SerialInterface_OnDataReceived(byte[] data)
        {
            foreach (Byte value in data)
            {
                list.Add(Convert.ToByte(value));
            }

            convertList();

        }

        private void convertList()
        {
            short nextValueAD = 0;
            ushort nextValuePIX = 0;
            byte[] byteArray = new byte[4];

            while (list.Count >= 5)
            {
                try
                {
                    if (Convert.ToByte(list[4]) != 0x0A)
                    {
                        throw new Exception("LF not found");
                    }

                    byteArray = list.GetRange(0, 4).ToArray();
                    list.RemoveRange(0, 5);
                    BitArray nextValueADBit = new BitArray(byteArray);
                    if (nextValueADBit[11] == true)
                    {
                        nextValueADBit = nextValueADBit.Or(new BitArray(System.BitConverter.GetBytes(0x0000F000)));
                    }
                    nextValueADBit.CopyTo(byteArray, 0);
                    nextValueAD = BitConverter.ToInt16(byteArray, 0);
                    nextValuePIX = BitConverter.ToUInt16(byteArray, 2);

                    nextValueIndex = (nextValueIndex < liveDataAD.Length - 1) ? nextValueIndex + 1 : 0;

                    //Calculation AD-Value
                    liveDataAD[nextValueIndex] = Convert.ToDouble(nextValueAD * 244.14 * Math.Pow(10, -6));
                    liveDataPIX[nextValueIndex] = Convert.ToDouble(nextValuePIX);

                    //beginTime = DateTime.Now;

                    Debug.WriteLine(nextValueAD);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "SerialInterface Event");
                    
                }
            }


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


        private void updateData()
        {
            if (chart1.InvokeRequired)
            {
                chart1.Invoke(new MethodInvoker(updateData));
            }
            else
            {
                chart1.Series["liveDataAD"].Points.RemoveAt(nextValueIndex);
                chart1.Series["liveDataPIX"].Points.RemoveAt(nextValueIndex);
                chart1.Series["liveDataAD"].Points.InsertY(nextValueIndex, liveDataAD[nextValueIndex]);
                chart1.Series["liveDataPIX"].Points.InsertY(nextValueIndex, liveDataPIX[nextValueIndex]);

                Debug.WriteLine(liveDataAD[nextValueIndex].ToString() + " " + liveDataPIX[nextValueIndex].ToString());
                debug.Text = Math.Round(liveDataAD[nextValueIndex], 4).ToString() + ";" + liveDataPIX[nextValueIndex].ToString();
                fileWriter.writeValue(liveDataAD[nextValueIndex].ToString() + ";" + liveDataPIX[nextValueIndex].ToString());
            }

        }

        private void Form1_FormClosing(Object sender, FormClosingEventArgs e)
        {
            fileWriter.closeFile();

            int number = 0;
            number = serialCom.closePort();
            Console.WriteLine(number);
        }

        private void trackBarRotation_Scroll(object sender, EventArgs e)
        {
            TrackBar trackBar = sender as TrackBar;

            numericUpDownRotation.Value = trackBar.Value;
        }

        private void numericUpDownRotation_ValueChanged(object sender, EventArgs e)
        {
            NumericUpDown rotationUpDown = sender as NumericUpDown;
            int number = 0;
            try
            {
                number = Convert.ToInt32(rotationUpDown.Value);
                if (number > 255)
                {
                    throw new ArgumentException();
                }
                trackBarRotation.Value = number;
            }
            catch(Exception ex)
            {
                rotationUpDown.Text = "0";
            }
            
        }

        private void trackBarLaser_Scroll(object sender, EventArgs e)
        {
            TrackBar trackBar = sender as TrackBar;

            numericUpDownLaser.Value = trackBar.Value;
        }

        private void numericUpDownLaser_ValueChanged(object sender, EventArgs e)
        {
            NumericUpDown rotationUpDown = sender as NumericUpDown;
            int number = 0;
            try
            {
                number = Convert.ToInt32(rotationUpDown.Value);
                if (number > 255)
                {
                    throw new ArgumentException();
                }
                trackBarLaser.Value = number;
            }
            catch (Exception ex)
            {
                rotationUpDown.Text = "0";
            }

        }

        private void laser_Click(object sender, EventArgs e)
        {
            int returnValue = 0;
            returnValue = serialCom.sendValues(Convert.ToByte(numericUpDownLaser.Value));
            switch (returnValue)
            {
                case 0:
                    pictureLaser.Image = Properties.Resources._269210;
                    laser.Text = "Off";
                    break;
                case 1:
                    pictureLaser.Image = Properties.Resources._269251;
                    laser.Text = "On";
                    break;
                case -1:
                    pictureLaser.Image = Properties.Resources._269210;
                    laser.Text = "Off";
                    break;
            }
        }

        private void domainUpDown3_SelectedItemChanged(object sender, EventArgs e)
        {
            DomainUpDown color = sender as DomainUpDown;
            switch (color.Text)
            {
                case "Red":
                    color.BackColor = Color.Red;
                    color.ForeColor = Color.WhiteSmoke;
                    patternColor = ColorPattern.Red;
                    break;
                case "Green":
                    color.BackColor = Color.Green;
                    color.ForeColor = Color.WhiteSmoke;
                    patternColor = ColorPattern.Green;
                    break;
                case "Blue":
                    color.BackColor = Color.Blue;
                    color.ForeColor = Color.WhiteSmoke;
                    patternColor = ColorPattern.Blue;
                    break;
                case "Cyan":
                    color.BackColor = Color.Cyan;
                    color.ForeColor = Color.Black;
                    patternColor = ColorPattern.Cyan;
                    break;
                case "White":
                    color.BackColor = Color.WhiteSmoke;
                    color.ForeColor = Color.Black;
                    patternColor = ColorPattern.White;
                    break;
                default:
                    color.BackColor = Color.Empty;
                    color.ForeColor = Color.Black;
                    patternColor = ColorPattern.none;
                    break;
            }
        }

        private void domainUpDown2_SelectedItemChanged(object sender, EventArgs e)
        {
            DomainUpDown pattern = sender as DomainUpDown;
            switch (pattern.Text)
            {
                case "No pattern":
                    patternDisplay = DisplayPattern.noPattern;
                    break;
                case "One touch":
                    patternDisplay = DisplayPattern.oneTouch;
                    break;
                case "Multi touch":
                    patternDisplay = DisplayPattern.multiTouch;
                    break;
                case "T pattern":
                    patternDisplay = DisplayPattern.tPattern;
                    break;
                default:
                    patternDisplay = DisplayPattern.noPattern;
                    break;
            }
        }

        private void patternButton_Click(object sender, EventArgs e)
        {
            serialCom.sendValues(patternDisplay, patternColor);
        }

        private void domainUpDown1_SelectedItemChanged(object sender, EventArgs e)
        {
            DomainUpDown rotation = sender as DomainUpDown;
            switch (rotation.Text)
            {
                case "Sample":
                    valueRotation = RotationValue.Sample;
                    break;
                case "Right":
                    valueRotation = RotationValue.Right;
                    break;
                case "Left":
                    valueRotation = RotationValue.Left;
                    break;
                default:
                    break;
            }
        }

        private void rotation_Click(object sender, EventArgs e)
        {
            serialCom.sendValues(valueRotation, Convert.ToByte(numericUpDownRotation.Value));
        }
    }
}