// SettingsDialog.cs
// Copyright (c) 2016, 2017, 2019 Kenneth Gober
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Windows.Forms;

namespace Emulator
{
    public partial class SettingsDialog : Form
    {
        private Boolean mOK;

        public SettingsDialog()
        {
            InitializeComponent();
        }

        public Boolean OK
        {
            get { return mOK; }
        }

        public Int32 ReceiveRate
        {
            get
            {
                if (radioButton1.Checked) return 110;
                if (radioButton2.Checked) return 150;
                if (radioButton3.Checked) return 300;
                if (radioButton4.Checked) return 600;
                if (radioButton5.Checked) return 1200;
                if (radioButton6.Checked) return 2400;
                if (radioButton7.Checked) return 2400;
                if (radioButton8.Checked) return 2400;
                if (radioButton9.Checked) return 1200;
                if (radioButton10.Checked) return 1200;
                if (radioButton11.Checked) return 0;
                return -1;
            }
        }

        public Int32 TransmitRate
        {
            get
            {
                if (radioButton1.Checked) return 110;
                if (radioButton2.Checked) return 150;
                if (radioButton3.Checked) return 300;
                if (radioButton4.Checked) return 600;
                if (radioButton5.Checked) return 1200;
                if (radioButton6.Checked) return 2400;
                if (radioButton7.Checked) return 150;
                if (radioButton8.Checked) return 110;
                if (radioButton9.Checked) return 150;
                if (radioButton10.Checked) return 110;
                if (radioButton11.Checked) return 0;
                return -1;
            }
        }

        public Parity Parity
        {
            get
            {
                if (radioButton12.Checked) return Parity.Mark;
                if (radioButton13.Checked) return Parity.Even;
                return Parity.None;
            }
        }

        public Boolean OptHalfDuplex
        {
            get
            {
                if (radioButton14.Checked) return false;
                if (radioButton15.Checked) return true;
                return false;
            }
            set
            {
                radioButton14.Checked = !value;
                radioButton15.Checked = value;
            }
        }

        public Boolean OptHalfASCII
        {
            get { return checkBox1.Checked; }
            set { checkBox1.Checked = value; }
        }

        public Boolean OptBackspaceSendsDEL
        {
            get { return checkBox2.Checked; }
            set { checkBox2.Checked = value; }
        }

        public Boolean OptAutoRepeat
        {
            get { return checkBox3.Checked; }
            set { checkBox3.Checked = value; }
        }

        public Boolean OptMarginBell
        {
            get { return checkBox4.Checked; }
            set { checkBox4.Checked = value; }
        }

        public Boolean OptStretchDisplay
        {
            get { return checkBox5.Checked; }
            set { checkBox5.Checked = value; }
        }

        private void SettingsDialog_Load(object sender, EventArgs e)
        {
            mOK = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            mOK = true;
        }
    }
}