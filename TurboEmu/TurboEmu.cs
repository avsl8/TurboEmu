﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;


namespace TurboEmu
{
    public partial class TurboEmu : Form
    {
        public static bool Debug = false;

        Emulation emulation = new Emulation();
        Log log = new Log();

        /* Movable Panel */
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        /* Settings */
        private string SettingsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\thud_emu";
        private string SettingsFile = "settings.json";
        private string SettingsAutostart = "autostart";
        private bool Autostart = false;

        Version EmuVersion = Assembly.GetEntryAssembly().GetName().Version;

        public TurboEmu()
        {
            InitializeComponent();
        }

        private void CBTrial_CheckedChanged(object sender, EventArgs e) =>
            emulation.Trial = CBTrial.Checked;

        private void CBAutostart_CheckedChanged(object sender, EventArgs e)
        {
            if (Autostart)
                return;
            try
            {
                string AutostartFile = Path.Combine(SettingsDir, SettingsAutostart);
                if (CBAutostart.Checked)
                {
                    if (!File.Exists(AutostartFile))
                    {
                        FileStream CreateAutostart = File.Create(AutostartFile);
                        CreateAutostart.Close();
                    }
                    MessageBox.Show("Delete the " + SettingsAutostart + " inside the \"thud_emu\" folder\nif you want to disable the autostart option.", "TurboEmu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    if (File.Exists(AutostartFile))
                        File.Delete(AutostartFile);
                }
            }
            catch (Exception ex)
            {
                log.Debug("Can't save " + SettingsAutostart + " Exception: " + ex.Message);
                MessageBox.Show("Can't save " + SettingsAutostart + "\n\nException:\n" + ex.Message, "TurboEmu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void CB_Package_SelectedIndexChanged(object sender, EventArgs e)
        {
            emulation.Tier1_Free = false;
            emulation.Tier2_Standard = false;
            emulation.Tier3_Unleashed = false;

            if (CB_Package.SelectedIndex == 0)
                emulation.Tier1_Free = true;
            else if (CB_Package.SelectedIndex == 1)
                emulation.Tier2_Standard = true;
            else if (CB_Package.SelectedIndex == 2)
                emulation.Tier3_Unleashed = true;
        }

        private void TurboEmu_FormClosing(object sender, FormClosingEventArgs e) =>
            SaveConfig();

        private void TurboEmu_Load(object sender, EventArgs e)
        {

            LB_Version.Text = string.Format(LB_Version.Text, EmuVersion.Major, EmuVersion.Minor, EmuVersion.Build);

#if (DEBUG)
            /* Enable DEBUG out only for Debug Release */
            Debug = true;
#endif

            /* Delete old logs and rename last one */
            if (File.Exists(Log.LogFile + ".old"))
                File.Delete(Log.LogFile + ".old");
            if (File.Exists(Log.LogFile))
                File.Move(Log.LogFile, Log.LogFile + ".old");

            if (!Directory.Exists(SettingsDir))
                Directory.CreateDirectory(SettingsDir);

            if (File.Exists(Path.Combine(SettingsDir, SettingsAutostart)))
                Autostart = true;

            /* Fix Proxy issue */
            emulation.UninstallProxy();

            log.Write("  ______           __          ______               ");
            log.Write(" /_  __/_  _______/ /_  ____  / ____/___ ___  __  __");
            log.Write("  / / / / / / ___/ __ \\/ __ \\/ __/ / __ `__ \\/ / / /");
            log.Write(" / / / /_/ / /  / /_/ / /_/ / /___/ / / / / / /_/ / ");
            log.Write("/_/  \\__,_/_/  /_.___/\\____/_____/_/ /_/ /_/\\__,_/  ");
            log.Write("Version: " + EmuVersion.Major + "." + EmuVersion.Minor + "." + EmuVersion.Build);
            if (File.Exists(Path.Combine(SettingsDir, SettingsFile)))
            {
                log.Write("Loading " + SettingsFile);

                string Settings = File.ReadAllText(Path.Combine(SettingsDir, SettingsFile), Encoding.UTF8);
                string AutostartFile = Path.Combine(SettingsDir, SettingsAutostart);
                try
                {
                    Dictionary<string, object> SettingsData = JsonConvert.DeserializeObject<Dictionary<string, object>>(Settings);
                    SettingsData.TryGetValue("tier1_free", out object objecttier1);
                    SettingsData.TryGetValue("tier2_standard", out object objecttier2);
                    SettingsData.TryGetValue("tier3_unleashed", out object objecttier3);
                    SettingsData.TryGetValue("trial_license", out object objecttrial);
                    SettingsData.TryGetValue("method", out object objectmethod);
                    SettingsData.TryGetValue("proxy_port", out object objectproxyport);

                    try
                    {
                        bool.TryParse(objecttier1.ToString(), out bool tier1);
                        bool.TryParse(objecttier2.ToString(), out bool tier2);
                        bool.TryParse(objecttier3.ToString(), out bool tier3);
                        bool.TryParse(objecttrial.ToString(), out bool trial);
                        try
                        {
                            Int32.TryParse(objectmethod.ToString(), out int method);
                            switch (method)
                            {
                                case 0:
                                    CB_Method.SelectedIndex = 0;
                                    break;
                                case 1:
                                    CB_Method.SelectedIndex = 1;
                                    break;
                                default:
                                    CB_Method.SelectedIndex = 1;
                                    break;
                            }
                        }
                        catch { CB_Method.SelectedIndex = 1; }

                        if (tier1)
                            CB_Package.SelectedIndex = 0;
                        if (tier2)
                            CB_Package.SelectedIndex = 1;
                        if (tier3)
                            CB_Package.SelectedIndex = 2;

                        if(!tier1 && !tier2 && !tier3)
                            CB_Package.SelectedIndex = 2;

                        CBTrial.Checked = trial;
                        if (objectproxyport != null)
                            int.TryParse(objectproxyport.ToString(), out emulation.ProxyPort);
                    }
                    catch (Exception)
                    {
                        log.Write("Delete corrupted " + SettingsFile);
                        File.Delete(Path.Combine(SettingsDir, SettingsFile));
                    }

                    if (File.Exists(AutostartFile))
                        CBAutostart.Checked = true;

                    if (Autostart)
                    {
                        log.Write("Autostart enabled");
                        ShowInTaskbar = false;
                        Visible = false;
                        emulation.StartTurboHUD();
                    }
                }
                catch (Exception)
                {
                    log.Write("Delete corrupted " + SettingsFile);
                    File.Delete(Path.Combine(SettingsDir, SettingsFile));
                }
            }
            else
            {
                log.Write("Loading default config");
                CB_Method.SelectedIndex = 1;
                CB_Package.SelectedIndex = 2;
            }
        }
        private void BT_Start_Click(object sender, EventArgs e)
        {
            ShowInTaskbar = false;
            Visible = false;
            SaveConfig();
            emulation.StartTurboHUD();
        }
        private void CB_Method_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CB_Method.SelectedIndex == 0)
            {
                emulation.Method = 0;
                LB_Connection.ForeColor = Color.DarkRed;
                LB_Connection.Text = "ros-bot.com is offline";
            }
            else if (CB_Method.SelectedIndex == 1)
            {
                emulation.Method = 1;
                LB_Connection.ForeColor = Color.Green;
                LB_Connection.Text = "ros-bot.com is online";
            }
        }

        private void LL_Website_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) =>
            Process.Start("http://turboemu.tk/");

        private void LabelExit_Click(object sender, EventArgs e)
        {
            emulation.StopTurboHUD();
            Application.Exit();
        }

        private void SaveConfig()
        {
            try
            {
                string ConfigFile = Path.Combine(SettingsDir, SettingsFile);
                log.Write("Save " + SettingsFile);
                Dictionary<string, object> SettingsData = new Dictionary<string, object>
                {
                    { "tier1_free", emulation.Tier1_Free }, // Tier 1
                    { "tier2_standard", emulation.Tier2_Standard }, // Tier 2
                    { "tier3_unleashed", emulation.Tier3_Unleashed }, // Tier 3
                    { "trial_license", emulation.Trial }, // Trial License
                    { "method", emulation.Method }, // Method
                    { "proxy_port", emulation.ProxyPort } // Use last Proxy Port
                };
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(ConfigFile, JsonConvert.SerializeObject(SettingsData), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                log.Write("Can't save " + SettingsFile + " Exception: " + ex.Message);
                MessageBox.Show("Can't save " + SettingsFile + "\n\nException:\n" + ex.Message, "TurboEmu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MovePanel(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

    }
}
