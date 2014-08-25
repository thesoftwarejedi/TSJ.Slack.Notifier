using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSJ.Slack.Notifier
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.FlashWindow = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.ShowBubbles = checkBox2.Checked;
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            checkBox2.Checked = Properties.Settings.Default.ShowBubbles;
            checkBox1.Checked = Properties.Settings.Default.FlashWindow;
        }
    }
}
