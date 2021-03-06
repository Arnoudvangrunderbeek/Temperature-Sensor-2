﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using ZedGraph;
using System.IO;
using System.Collections;
using test;

namespace TemperatureReader
{
    public partial class Form1 : Form
    {
        int tickStart;

        Boolean first = true;

        TextWriter tw;

        DataUpload _up;

        Hashtable htVals;

        const String OUTLET_NAME = "router_outlet";

        public Form1()
        {
            InitializeComponent();
            
            InitGraph();

            serialPort1.Open();

            tw = new StreamWriter("data" + System.DateTime.Now.ToFileTime().ToString() + ".txt");

            //Make the new data upload ""thingy""
            _up = new DataUpload();
            //Initialize it with the system name string. ie Jason_House . .no spaces for now
            _up.Init("jason_home2");
            //If you have python and apache installed, you can run it locally
            _up.setLocalDebug(false);
            htVals = new Hashtable();
            htVals.Add(OUTLET_NAME, 0);


        }

        private void InitGraph()
        {
            GraphPane myPane = zedGraphControl1.GraphPane;
            myPane.Title.Text = "Temperature Data";
            myPane.XAxis.Title.Text = "Time, Seconds";
            myPane.YAxis.Title.Text = "Degrees C";

            // Save 1200 points.  At 50 ms sample rate, this is one minute
            // The RollingPointPairList is an efficient storage class that always
            // keeps a rolling set of point data without needing to shift any data values
            RollingPointPairList list = new RollingPointPairList(1200);
            RollingPointPairList list2 = new RollingPointPairList(1200);

            // Initially, a curve is added with no data points (list is empty)
            // Color is blue, and there will be no symbols
            LineItem curve = myPane.AddCurve("power 1", list, Color.Blue, SymbolType.None);
            LineItem curve2 = myPane.AddCurve("power 2", list2, Color.Red, SymbolType.None);


            // Just manually control the X axis range so it scrolls continuously
            // instead of discrete step-sized jumps
            myPane.XAxis.Scale.Min = 0;
            myPane.XAxis.Scale.Max = 30;
            myPane.XAxis.Scale.MinorStep = 1;
            myPane.XAxis.Scale.MajorStep = 5;

            // Scale the axes
            zedGraphControl1.AxisChange();

            // Save the beginning time for reference
            tickStart = Environment.TickCount;

        }

        private void updateGraph(double newValue, int index)
        {
            // Make sure that the curvelist has at least one curve
            if (zedGraphControl1.GraphPane.CurveList.Count <= index)
                return;

            // Get the first CurveItem in the graph
            LineItem curve = zedGraphControl1.GraphPane.CurveList[index] as LineItem;
            if (curve == null)
                return;

            // Get the PointPairList
            IPointListEdit list = curve.Points as IPointListEdit;
            // If this is null, it means the reference at curve.Points does not
            // support IPointListEdit, so we won't be able to modify it
            if (list == null)
                return;

            // Time is measured in seconds
            double time = (Environment.TickCount - tickStart) / 1000.0;

            // 3 seconds per cycle
            list.Add(time, newValue);

            // Keep the X scale at a rolling 30 second interval, with one
            // major step between the max X value and the end of the axis
            Scale xScale = zedGraphControl1.GraphPane.XAxis.Scale;
            if (time > xScale.Max - xScale.MajorStep)
            {
                xScale.Max = time + xScale.MajorStep;
                xScale.Min = xScale.Max - 30.0;
            }

            // Make sure the Y axis is rescaled to accommodate actual data
            zedGraphControl1.AxisChange();
            // Force a redraw
            zedGraphControl1.Invalidate();
        }

        private void getCoordinatorTemp()
        {
            writeSerialPort("8");

            Thread.Sleep(200);
            String message = serialPort1.ReadExisting();
            message = message.Substring(message.IndexOf(",") + 1, 4);
            String upper = message.Substring(0, 2);
            String lower = message.Substring(2, 2);
            Byte[] wordArray = new byte[2];
            wordArray[0] = byte.Parse(lower, System.Globalization.NumberStyles.HexNumber);
            wordArray[1] = byte.Parse(upper, System.Globalization.NumberStyles.HexNumber);
            Int16 tempInt = BitConverter.ToInt16(wordArray, 0);
            double tempDouble = (0.0625) * (double)(tempInt >> 3);

            txtTemperature.Text = tempDouble.ToString();

            Console.WriteLine(OUTLET_NAME + ": " + tempDouble);

            updateGraph(tempDouble, 0);
        }

        private void getRouterTemp(int deviceId)
        {
            writeSerialPort("2");
            Thread.Sleep(200);
            writeSerialPort("03");
            Thread.Sleep(200);

            String tempStr;
            if (deviceId == 0)
            {
                tempStr = "0001";
            }
            else
            {
                tempStr = "143E";
            }
            writeSerialPort(tempStr);

            Thread.Sleep(1500);
            String message = serialPort1.ReadExisting();

            String message2 = message.Substring(message.Length - 8, 6);
            Console.WriteLine(message);

            String upper = message2.Substring(0, 2);
            String middle = message2.Substring(2, 2);
            String lower = message2.Substring(4, 2);

            byte[] wordArray = new byte[4];
            wordArray[0] = byte.Parse(lower, System.Globalization.NumberStyles.HexNumber);
            wordArray[1] = byte.Parse(middle, System.Globalization.NumberStyles.HexNumber);
            wordArray[2] = byte.Parse(upper, System.Globalization.NumberStyles.HexNumber);
            wordArray[3] = 0;
            UInt32 tempInt = BitConverter.ToUInt32(wordArray, 0);
            Double tempDouble = (double)tempInt;

            
            txtRouterTemp.Text = tempDouble.ToString();

            htVals[OUTLET_NAME + tempStr] = tempInt;
            _up.UploadData(DateTime.Now, htVals);

            if (first)
            {
                first = false;
            }
            else
            {
                updateGraph(tempDouble, deviceId);
                tw.WriteLine(tempDouble);
                tw.Flush();
            }
            Console.WriteLine(OUTLET_NAME + ": " + tempDouble);

        }

        private void writeSerialPort(String writeStr)
        {
            writeSerialPort(writeStr, false);
           
        }

        private void writeSerialPort(String writeStr, Boolean lineFeed)
        {
            for (int i = 0; i < writeStr.Length; i++)
            {
                serialPort1.Write(writeStr.Substring(i, 1));
                Thread.Sleep(20);
            }

            if (lineFeed)
            {
                serialPort1.WriteLine("");
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                getRouterTemp(0);
                getRouterTemp(1);

               
            }
            catch (Exception)
            {
                writeSerialPort("",true);
                writeSerialPort("", true);
                writeSerialPort("", true);
                writeSerialPort("", true);
                writeSerialPort("", true);
                writeSerialPort("", true);
                Thread.Sleep(10000);
                serialPort1.ReadExisting();
            }



        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            serialPort1.Close();
            tw.Flush();
            tw.Close();
        }

         
    }
}
