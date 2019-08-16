﻿using HipHopFile;
using RenderWareFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using static IndustrialPark.Models.BSP_IO_Shared;
using static IndustrialPark.Models.Model_IO_Assimp;

namespace IndustrialPark
{
    public partial class ImportModel : Form
    {
        public ImportModel()
        {
            InitializeComponent();
            
            buttonOK.Enabled = false;
            TopMost = true;
        }

        List<string> filePaths = new List<string>();

        private void buttonImportRawData_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Multiselect = true,
                Filter = GetImportFilter()
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                foreach (string s in openFileDialog.FileNames)
                    filePaths.Add(s);

                UpdateListBox();
            }
        }

        private void UpdateListBox()
        {
            listBox1.Items.Clear();

            foreach (string s in filePaths)
                listBox1.Items.Add(Path.GetFileName(s));

            buttonOK.Enabled = listBox1.Items.Count > 0;
        }
        
        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                filePaths.RemoveAt(listBox1.SelectedIndex);
                UpdateListBox();
            }
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            Close();
        }

        public static List<Section_AHDR> GetAssets(Game game, out bool success, out bool overwrite, out bool simps, out bool piptVcolors)
        {
            ImportModel a = new ImportModel();
            if (a.ShowDialog() == DialogResult.OK)
            {
                List<Section_AHDR> AHDRs = new List<Section_AHDR>();
                simps = a.checkBoxGenSimps.Checked;

                if (simps)
                    MessageBox.Show("a SIMP for each imported MODL will be generated and placed on a new DEFAULT layer.");

                foreach (string filePath in a.filePaths)
                {
                    string assetName = Path.GetFileNameWithoutExtension(filePath) + ".dff";
                    AssetType assetType = AssetType.MODL;
                    byte[] assetData = Path.GetExtension(filePath).ToLower().Equals(".dff") ?
                                File.ReadAllBytes(filePath) :
                                ReadFileMethods.ExportRenderWareFile(
                                    CreateDFFFromAssimp(filePath,
                                    a.checkBoxFlipUVs.Checked),
                                    currentRenderWareVersion(game));
                    
                    AHDRs.Add(
                        new Section_AHDR(
                            Functions.BKDRHash(assetName),
                            assetType,
                            ArchiveEditorFunctions.AHDRFlagsFromAssetType(assetType),
                            new Section_ADBG(0, assetName, "", 0),
                            assetData));
                }

                success = true;
                overwrite = a.checkBoxOverwrite.Checked;
                piptVcolors = a.checkBoxEnableVcolors.Checked;
                return AHDRs;
            }
            else
            {
                success = overwrite = simps = piptVcolors = false;
                return null;
            }
        }
    }
}
