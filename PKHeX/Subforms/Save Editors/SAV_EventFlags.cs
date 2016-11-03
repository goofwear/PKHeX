﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PKHeX
{
    public partial class SAV_EventFlags : Form
    {
        public SAV_EventFlags()
        {
            InitializeComponent();

            DragEnter += tabMain_DragEnter;
            DragDrop += tabMain_DragDrop;

            flags = SAV.EventFlags;
            Constants = SAV.EventConsts;

            CB_Stats.Items.Clear();
            for (int i = 0; i < Constants.Length; i++)
                CB_Stats.Items.Add(i.ToString());

            TLP_Flags.SuspendLayout();
            TLP_Const.SuspendLayout();
            TLP_Flags.Scroll += Util.PanelScroll;
            TLP_Const.Scroll += Util.PanelScroll;
            TLP_Flags.Controls.Clear();
            TLP_Const.Controls.Clear();
            addFlagList(getStringList("flags"));
            addConstList(getStringList("const"));

            TLP_Flags.ResumeLayout();
            TLP_Const.ResumeLayout();

            Util.TranslateInterface(this, Main.curlanguage);
            
            CB_Stats.SelectedIndex = 0;
            nud.Maximum = flags.Length - 1;
            nud.Text = "0";
        }

        private readonly SaveFile SAV = Main.SAV.Clone();
        private readonly bool[] flags;
        private readonly ushort[] Constants;
        private const string flagTag = "bool_";
        private const string constTag = "const_";
        private const string flagLabelTag = "flag_";
        private const string constLabelTag = "L_";
        private bool editing;
        private int constEntry = -1;

        private void B_Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void B_Save_Click(object sender, EventArgs e)
        {
            // Gather Updated Flags
            foreach (CheckBox flag in TLP_Flags.Controls.OfType<CheckBox>())
                flags[getControlNum(flag)] = flag.Checked;
            SAV.EventFlags = flags;

            // Copy back Constants
            changeConstantIndex(null, null); // Trigger Saving
            SAV.EventConsts = Constants;
            Array.Copy(SAV.Data, Main.SAV.Data, SAV.Data.Length);
            Close();
        }

        private string[] getStringList(string type)
        {
            string[] text = null;
            if (SAV.ORAS)
                text = Util.getStringList($"{type}_oras");
            else if (SAV.XY)
                text = Util.getStringList($"{type}_xy");
            return text;
        }
        private void addFlagList(string[] list)
        {
            if (list == null || list.Length == 0)
            {
                TLP_Flags.Controls.Add(new Label { Text = "Needs more research.", Name = "TLP_Flags_Research", ForeColor = Color.Red, AutoSize = true }, 0, 0);
                return;
            }

            // Get list
            List<int> num = new List<int>();
            List<string> desc = new List<string>();

            foreach (string[] split in list.Select(s => s.Split('\t')).Where(split => split.Length == 2))
            {
                try
                {
                    int n = Convert.ToInt32(split[0]);
                    if (num.Contains(n))
                        continue;
                    num.Add(n);
                    desc.Add(split[1]);
                } catch { }
            }
            if (num.Count == 0)
            {
                TLP_Flags.Controls.Add(new Label { Text = "Needs more research.", Name = "TLP_Flags_Research", ForeColor = Color.Red, AutoSize = true }, 0, 0);
                return;
            }

            for (int i = 0; i < num.Count; i++)
            {
                var lbl = new Label
                {
                    Text = desc[i],
                    Name = flagLabelTag + num[i].ToString("0000"),
                    Margin = Padding.Empty,
                    AutoSize = true
                };
                var chk = new CheckBox
                {
                    Name = flagTag + num[i].ToString("0000"),
                    CheckAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty,
                    Checked = flags[num[i]],
                    AutoSize = true
                };
                chk.CheckStateChanged += toggleFlag;
                lbl.Click += (sender, e) => { chk.Checked ^= true; };
                TLP_Flags.Controls.Add(chk, 0, i);
                TLP_Flags.Controls.Add(lbl, 1, i);
            }
        }
        private void addConstList(string[] list)
        {
            if (list == null || list.Length == 0)
            {
                TLP_Const.Controls.Add(new Label { Text = "Needs more research.", Name = "TLP_Const_Research", ForeColor = Color.Red, AutoSize = true }, 0, 0);
                return;
            }

            // Get list
            List<int> num = new List<int>();
            List<string> desc = new List<string>();

            foreach (string[] split in list.Select(s => s.Split('\t')).Where(split => split.Length == 2))
            {
                try
                {
                    int n = Convert.ToInt32(split[0]);
                    if (num.Contains(n))
                        continue;
                    num.Add(n);
                    desc.Add(split[1]);
                } catch { }
            }
            if (num.Count == 0)
            {
                TLP_Const.Controls.Add(new Label { Text = "Needs more research.", Name = "TLP_Const_Research", ForeColor = Color.Red, AutoSize = true }, 0, 0);
                return;
            }

            for (int i = 0; i < num.Count; i++)
            {
                var lbl = new Label
                {
                    Text = desc[i],
                    Name = constLabelTag + num[i].ToString("0000"),
                    Margin = Padding.Empty,
                    AutoSize = true
                };
                var mtb = new NumericUpDown
                {
                    Maximum = ushort.MaxValue,
                    Minimum = ushort.MinValue,
                    Value = Constants[num[i]],
                    Name = constTag + num[i].ToString("0000"),
                    Margin = Padding.Empty,
                    Width = 55,
                };
                mtb.TextChanged += toggleConst;
                TLP_Const.Controls.Add(mtb, 0, i);
                TLP_Const.Controls.Add(lbl, 1, i);
            }
        }

        private int getControlNum(Control chk)
        {
            try
            {
                string source = chk.Name;
                return Convert.ToInt32(source.Substring(Math.Max(0, source.Length - 4)));
            }
            catch { return 0; }
        }
        private void changeCustomBool(object sender, EventArgs e)
        {
            if (editing)
                return;
            editing = true;
            flags[(int)nud.Value] = c_CustomFlag.Checked;
            CheckBox c = TLP_Flags.Controls[flagTag + nud.Value.ToString("0000")] as CheckBox;
            if (c != null)
            {
                c.Checked = c_CustomFlag.Checked;
            }
            editing = false;
        }
        private void changeCustomFlag(object sender, EventArgs e)
        {
            int flag = (int)nud.Value;
            if (flag >= flags.Length)
            {
                c_CustomFlag.Checked = false;
                c_CustomFlag.Enabled = false;
                nud.BackColor = Color.Red;
            }
            else
            {
                c_CustomFlag.Enabled = true;
                nud.ResetBackColor();
                c_CustomFlag.Checked = flags[flag];
            }
        }
        private void changeCustomFlag(object sender, KeyEventArgs e)
        {
            changeCustomFlag(null, (EventArgs)e);
        }
        private void toggleFlag(object sender, EventArgs e)
        {
            if (editing)
                return;
            editing = true;
            int flagnum = getControlNum((CheckBox) sender);
            flags[flagnum] = ((CheckBox)sender).Checked;
            if (nud.Value == flagnum)
                c_CustomFlag.Checked = flags[flagnum];
            editing = false;
        }
        
        private void changeCustomConst(object sender, EventArgs e)
        {
            if (editing)
                return;
            editing = true;

            editing = true;
            Constants[CB_Stats.SelectedIndex] = (ushort)(Util.ToUInt32(((MaskedTextBox)sender).Text) & 0xFFFF);
            MaskedTextBox m = TLP_Flags.Controls[constTag + CB_Stats.SelectedIndex.ToString("0000")] as MaskedTextBox;
            if (m != null)
                m.Text = MT_Stat.Text;

            editing = false;
        }
        private void changeConstantIndex(object sender, EventArgs e)
        {
            if (constEntry > -1) // Set Entry
                Constants[constEntry] = (ushort)Math.Min(Util.ToUInt32(MT_Stat.Text), 0xFFFF);

            constEntry = CB_Stats.SelectedIndex; // Get Entry
            MT_Stat.Text = Constants[constEntry].ToString();
        }
        private void toggleConst(object sender, EventArgs e)
        {
            if (editing)
                return;

            editing = true;
            int constnum = getControlNum((NumericUpDown)sender);
            Constants[constnum] = (ushort)(Util.ToUInt32(((NumericUpDown)sender).Text) & 0xFFFF);
            if (constnum == CB_Stats.SelectedIndex)
                MT_Stat.Text = Constants[constnum].ToString();
            editing = false;
        }

        private void changeSAV(object sender, EventArgs e)
        {
            if (TB_NewSAV.Text.Length > 0 && TB_OldSAV.Text.Length > 0)
                diffSaves();
        }
        private void openSAV(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
                loadSAV(sender, ofd.FileName);
        }
        private void loadSAV(object sender, string path)
        {
            if (sender == B_LoadOld)
                TB_OldSAV.Text = path;
            else
                TB_NewSAV.Text = path;
        }
        private void diffSaves()
        {
            if (!File.Exists(TB_OldSAV.Text)) { Util.Alert("Save 1 path invalid."); return; }
            if (!File.Exists(TB_NewSAV.Text)) { Util.Alert("Save 2 path invalid."); return; }
            if (new FileInfo(TB_OldSAV.Text).Length > 0x100000) { Util.Alert("Save 1 file invalid."); return; }
            if (new FileInfo(TB_NewSAV.Text).Length > 0x100000) { Util.Alert("Save 2 file invalid."); return; }

            SaveFile s1 = SaveUtil.getVariantSAV(File.ReadAllBytes(TB_OldSAV.Text));
            SaveFile s2 = SaveUtil.getVariantSAV(File.ReadAllBytes(TB_NewSAV.Text));

            if (s1.GetType() != s2.GetType()) { Util.Alert("Save types are different.", $"S1: {s1.GetType().Name}", $"S2: {s2.GetType().Name}"); return; }
            if (s1.Version != s2.Version) { Util.Alert("Save versions are different.", $"S1: {s1.Version}", $"S2: {s2.Version}"); return; }

            string tbIsSet = "";
            string tbUnSet = "";
            try
            {
                bool[] oldBits = s1.EventFlags;
                bool[] newBits = s2.EventFlags;
                if (oldBits.Length != newBits.Length)
                { Util.Alert("Event flag lengths for games are different.", $"S1: {(GameVersion)s1.Game}", $"S2: {(GameVersion)s2.Game}"); return; }

                for (int i = 0; i < oldBits.Length; i++)
                {
                    if (oldBits[i] == newBits[i]) continue;
                    if (newBits[i])
                        tbIsSet += i.ToString("0000") + ",";
                    else
                        tbUnSet += i.ToString("0000") + ",";
                }
            }
            catch (Exception e)
            {
                Util.Error("An unexpected error has occurred.", e);
                Console.Write(e);
            }
            TB_IsSet.Text = tbIsSet;
            TB_UnSet.Text = tbUnSet;

            string r = "";
            try
            {
                ushort[] oldConst = s1.EventConsts;
                ushort[] newConst = s2.EventConsts;
                if (oldConst.Length != newConst.Length)
                { Util.Alert("Event flag lengths for games are different.", $"S1: {(GameVersion)s1.Game}", $"S2: {(GameVersion)s2.Game}"); return; }

                for (int i = 0; i < newConst.Length; i++)
                    if (oldConst[i] != newConst[i])
                        r += $"{i}: {oldConst[i]}->{newConst[i]}{Environment.NewLine}";
            }
            catch (Exception e)
            {
                Util.Error("An unexpected error has occurred.", e);
                Console.Write(e);
            }

            if (DialogResult.Yes != Util.Prompt(MessageBoxButtons.YesNo, "Copy Event Constant diff to clipboard?"))
                return;
            Clipboard.SetText(r);
        }

        private void tabMain_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }
        private void tabMain_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            loadSAV(Util.Prompt(MessageBoxButtons.YesNo, "FlagDiff Researcher:", "Yes: Old Save" + Environment.NewLine + "No: New Save") == DialogResult.Yes ? B_LoadOld : B_LoadNew, files[0]);
        }
    }
}