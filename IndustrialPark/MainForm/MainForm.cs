﻿using Microsoft.WindowsAPICodePack.Dialogs;
using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;

namespace IndustrialPark
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            StartPosition = FormStartPosition.CenterScreen;

            InitializeComponent();

#if !DEBUG
            addTXDArchiveToolStripMenuItem.Visible = false;
#endif

            uIToolStripMenuItem_Click(null, null);
            uIFTToolStripMenuItem_Click(null, null);

            ArchiveEditorFunctions.PopulateTemplateMenusAt(toolStripMenuItem_Templates, TemplateToolStripItemClick);

            renderer = new SharpRenderer(renderPanel);
        }

        private void StartRenderer()
        {
//#if !DEBUG
            new Thread(() =>
            {
//#endif
                if (InvokeRequired)
                    Invoke(new StartLoop(renderer.RunMainLoop), renderPanel);
                else
                    renderer.RunMainLoop(renderPanel);
//#if !DEBUG
            }).Start();
//#endif
        }
        
        public static string pathToSettings => Path.Combine(Application.StartupPath, "ip_settings.json");
        private string currentProjectPath;
        public string userTemplatesFolder => Path.Combine(Application.StartupPath, "Resources", "UserTemplates");

        private void MainForm_Load(object sender, EventArgs e)
        {
            UpdateUserTemplateComboBox();

            if (File.Exists(pathToSettings))
            {
                IPSettings settings = JsonConvert.DeserializeObject<IPSettings>(File.ReadAllText(pathToSettings));

                autoSaveOnClosingToolStripMenuItem.Checked = settings.AutosaveOnClose;
                autoLoadOnStartupToolStripMenuItem.Checked = settings.AutoloadOnStartup;
                checkForUpdatesOnStartupToolStripMenuItem.Checked = settings.CheckForUpdatesOnStartup;

                if (settings.CheckForUpdatesOnStartup && AutomaticUpdater.UpdateIndustrialPark(out _))
                {
                    Close();
                    Process.Start(Path.Combine(Application.StartupPath, "IndustrialPark.exe"));
                }
                
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length > 1)
                {
                    if (Path.GetExtension(args[1]).ToLower() == ".hip" || Path.GetExtension(args[1]).ToLower() == ".hop")
                    {
                        AddArchiveEditor(args[1]);
                        SetProjectToolStripStatusLabel();
                        StartRenderer();
                        return;
                    }
                }

                if (settings.AutoloadOnStartup && !string.IsNullOrEmpty(settings.LastProjectPath) && File.Exists(settings.LastProjectPath))
                    ApplySettings(settings.LastProjectPath);
            }
            else
            {
                MessageBox.Show(
                    "It appears this is your first time using Industrial Park.\n" +
                    "Please consult the documentation on the BFBB Modding Wiki to understand how to use the tool if you haven't already.\n" +
                    "Also, be sure to check individual asset pages if you're not sure what one of them or their settings do."
                );
                Program.AboutBox.Show();
            }

            SetProjectToolStripStatusLabel();
            StartRenderer();
        }

        private delegate void StartLoop(Panel renderPanel);
        
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (UnsavedChanges())
            {
                DialogResult result = notifyUnsavedChanges();
                if (result == DialogResult.Yes)
                    SaveAllChanges();
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (autoSaveOnClosingToolStripMenuItem.Checked)
                SaveProject(currentProjectPath);

            IPSettings settings = new IPSettings
            {
                AutosaveOnClose = autoSaveOnClosingToolStripMenuItem.Checked,
                AutoloadOnStartup = autoLoadOnStartupToolStripMenuItem.Checked,
                LastProjectPath = currentProjectPath,
                CheckForUpdatesOnStartup = checkForUpdatesOnStartupToolStripMenuItem.Checked
            };

            File.WriteAllText(pathToSettings, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
                try
                {
                    AddArchiveEditor(file);
                }
                catch (Exception ex)
                {
                    TopMost = true;
                    MessageBox.Show("Error opening file: " + ex.Message);
                    TopMost = false;
                }
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private DialogResult notifyUnsavedChanges() {
            TopMost = true;
            DialogResult result = MessageBox.Show(
                "You appear to have unsaved changes in one of your Archive Editors. Do you wish to save them before closing?", 
                "Warning", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning
            );
            TopMost = false;
            return result;
        }

        private void newToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (UnsavedChanges())
            {
                DialogResult result = notifyUnsavedChanges();
                if (result == DialogResult.Yes)
                    SaveAllChanges();
                else if (result == DialogResult.Cancel)
                    return;
            }

            currentProjectPath = null;
            ApplySettings(new ProjectJson());
            SetProjectToolStripStatusLabel();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (UnsavedChanges())
            {
                DialogResult result = notifyUnsavedChanges();
                if (result == DialogResult.Yes)
                    SaveAllChanges();
                else if (result == DialogResult.Cancel)
                    return;
            }

            OpenFileDialog openFile = new OpenFileDialog()
            { Filter = "JSON files|*.json" };

            if (openFile.ShowDialog() == DialogResult.OK)
                ApplySettings(openFile.FileName);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(currentProjectPath))
                SaveProject();
            else
                saveAsToolStripMenuItem_Click(null, null);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFile = new SaveFileDialog()
            { Filter = "JSON files|*.json" };

            if (saveFile.ShowDialog() == DialogResult.OK)
                SaveProject(saveFile.FileName);
        }

        private void SaveProject(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = Path.Combine(Application.StartupPath, "default_project.json");
            currentProjectPath = fileName;
            SaveProject(); 
        }

        private void SaveProject()
        {
            File.WriteAllText(currentProjectPath, JsonConvert.SerializeObject(FromCurrentInstance(), Formatting.Indented));
            SetProjectToolStripStatusLabel();
        }

        private void autoLoadOnStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autoLoadOnStartupToolStripMenuItem.Checked = !autoLoadOnStartupToolStripMenuItem.Checked;
        }

        private void autoSaveOnClosingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autoSaveOnClosingToolStripMenuItem.Checked = !autoSaveOnClosingToolStripMenuItem.Checked;
        }

        private void CheckForUpdatesOnStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            checkForUpdatesOnStartupToolStripMenuItem.Checked = !checkForUpdatesOnStartupToolStripMenuItem.Checked;
        }

        private void CheckForUpdatesNowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AutomaticUpdater.UpdateIndustrialPark(out bool hasChecked))
            {
                Close();
                Process.Start(Path.Combine(Application.StartupPath, "IndustrialPark.exe"));
            }
            else if (hasChecked)
                MessageBox.Show("No update found.");
        }

        private void CheckForUpdatesOnEditorFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!AutomaticUpdater.VerifyEditorFiles())
                MessageBox.Show("No update found.");
        }

        private void DownloadIndustrialParkEditorFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AutomaticUpdater.DownloadEditorFiles();
        }

        public ProjectJson FromCurrentInstance()
        {
            List<string> hips = new List<string>();
            List<HipHopFile.Platform> platforms = new List<HipHopFile.Platform>();
            List<uint> hiddenAssets = new List<uint>();

            foreach (ArchiveEditor ae in archiveEditors)
            {
                hips.Add(ae.GetCurrentlyOpenFileName());
                platforms.Add(ae.archive.platform);
                hiddenAssets.AddRange(ae.archive.GetHiddenAssets());
            }

            return new ProjectJson(hips, platforms, TextureManager.OpenTextureFolders.ToList(), renderer.Camera.Position,
                renderer.Camera.Yaw, renderer.Camera.Pitch, renderer.Camera.Speed, renderer.Camera.SpeedRot, renderer.Camera.FieldOfView,
                renderer.Camera.FarPlane, noCullingCToolStripMenuItem.Checked, wireframeFToolStripMenuItem.Checked, renderer.backgroundColor,
                renderer.normalColor, renderer.trigColor, renderer.mvptColor, renderer.sfxColor, useLegacyAssetIDFormatToolStripMenuItem.Checked,
                alternateNamingMode, hiddenAssets, renderer.isDrawingUI, ArchiveEditorFunctions.Grid, AssetMODL.renderBasedOnLodt, AssetMODL.renderBasedOnPipt,
                AssetJSP.dontRender, AssetBOUL.dontRender, AssetBUTN.dontRender, AssetCAM.dontRender, AssetDSTR_Scooby.dontRender, AssetDYNA.dontRender,
                AssetEGEN.dontRender, AssetHANG.dontRender, AssetLITE.dontRender, AssetMRKR.dontRender, AssetMVPT_Scooby.dontRender, AssetPEND.dontRender,
                AssetPLAT.dontRender, AssetPLAT.dontRender, AssetPLYR.dontRender, AssetSFX.dontRender, AssetSIMP.dontRender, AssetTRIG.dontRender,
                AssetUI.dontRender, AssetUIFT.dontRender, AssetVIL.dontRender, ArchiveEditorFunctions.persistentShinies, ArchiveEditorFunctions.hideHelp);
        }

        private void ApplySettings(string ipSettingsPath)
        {
            currentProjectPath = ipSettingsPath;
            SetProjectToolStripStatusLabel();
            ApplySettings(JsonConvert.DeserializeObject<ProjectJson>(File.ReadAllText(ipSettingsPath)));
        }

        private void ApplySettings(ProjectJson ipSettings)
        {
            TextureManager.ClearTextures();

            List<ArchiveEditor> archiveEditors = new List<ArchiveEditor>();
            archiveEditors.AddRange(this.archiveEditors);
            foreach (ArchiveEditor ae in archiveEditors)
                ae.CloseArchiveEditor();

            TextureManager.LoadTexturesFromFolder(ipSettings.TextureFolderPaths);
            
            ArchiveEditorFunctions.hiddenAssets = ipSettings.hiddenAssets;
            ArchiveEditorFunctions.hideHelp = ipSettings.hideHelp;

            for (int i = 0; i < ipSettings.hipPaths.Count; i++)
                if (ipSettings.hipPaths[i] == "Empty")
                    AddArchiveEditor();
                else
                {
                    if (File.Exists(ipSettings.hipPaths[i]))
                        AddArchiveEditor(ipSettings.hipPaths[i], ipSettings.scoobyPlatforms[i]);
                    else
                    {
                        TopMost = true;
                        MessageBox.Show("Error opening " + ipSettings.hipPaths[i] + ": file not found");
                        TopMost = false;
                    }
                }

            ArchiveEditorFunctions.hiddenAssets.Clear();

            renderer.Camera.SetPosition(ipSettings.CamPos);
            renderer.Camera.Yaw = ipSettings.Yaw;
            renderer.Camera.Pitch = ipSettings.Pitch;
            renderer.Camera.Speed = ipSettings.Speed;
            renderer.Camera.SpeedRot = ipSettings.Speed;
            renderer.Camera.FieldOfView = ipSettings.FieldOfView;
            renderer.Camera.FarPlane = ipSettings.FarPlane;
            ArchiveEditorFunctions.Grid = ipSettings.Grid;

            noCullingCToolStripMenuItem.Checked = ipSettings.NoCulling;
            if (noCullingCToolStripMenuItem.Checked)
                renderer.device.SetNormalCullMode(CullMode.None);
            else
                renderer.device.SetNormalCullMode(CullMode.Back);

            wireframeFToolStripMenuItem.Checked = ipSettings.Wireframe;
            if (wireframeFToolStripMenuItem.Checked)
                renderer.device.SetNormalFillMode(FillMode.Wireframe);
            else
                renderer.device.SetNormalFillMode(FillMode.Solid);

            renderer.backgroundColor = ipSettings.BackgroundColor;
            renderer.SetWidgetColor(ipSettings.WidgetColor);
            renderer.SetMvptColor(ipSettings.MvptColor);
            renderer.SetTrigColor(ipSettings.TrigColor);
            renderer.SetSfxColor(ipSettings.SfxColor);

            useLegacyAssetIDFormatToolStripMenuItem.Checked = ipSettings.UseLegacyAssetIDFormat;
            alternateNamingMode = ipSettings.AlternateNameDisplayMode;

            assetIDAssetNameToolStripMenuItem.Checked = alternateNamingMode;
            assetNameAssetIDToolStripMenuItem.Checked = !alternateNamingMode;

            uIModeToolStripMenuItem.Checked = ipSettings.isDrawingUI;
            renderer.isDrawingUI = ipSettings.isDrawingUI;

            useMaxRenderDistanceToolStripMenuItem.Checked = ipSettings.renderBasedOnLodt;
            AssetMODL.renderBasedOnLodt = ipSettings.renderBasedOnLodt;

            usePIPTForRenderingToolStripMenuItem.Checked = ipSettings.renderBasedOnPipt;
            AssetMODL.renderBasedOnPipt = ipSettings.renderBasedOnPipt;

            levelModelToolStripMenuItem.Checked = !ipSettings.dontRenderLevelModel;
            AssetJSP.dontRender = ipSettings.dontRenderLevelModel;

            bOULToolStripMenuItem.Checked = !ipSettings.dontRenderBOUL;
            AssetBOUL.dontRender = ipSettings.dontRenderBOUL;

            bUTNToolStripMenuItem.Checked = !ipSettings.dontRenderBUTN;
            AssetBUTN.dontRender = ipSettings.dontRenderBUTN;

            cAMToolStripMenuItem.Checked = !ipSettings.dontRenderCAM;
            AssetCAM.dontRender = ipSettings.dontRenderCAM;

            dSTRToolStripMenuItem.Checked = !ipSettings.dontRenderDSTR;
            AssetDSTR.dontRender = ipSettings.dontRenderDSTR;

            dYNAToolStripMenuItem.Checked = !ipSettings.dontRenderDYNA;
            AssetDYNA.dontRender = ipSettings.dontRenderDYNA;

            eGENToolStripMenuItem.Checked = !ipSettings.dontRenderEGEN;
            AssetEGEN.dontRender = ipSettings.dontRenderEGEN;

            hANGToolStripMenuItem.Checked = !ipSettings.dontRenderHANG;
            AssetHANG.dontRender = ipSettings.dontRenderHANG;

            lITEToolStripMenuItem.Checked = !ipSettings.dontRenderLITE;
            AssetLITE.dontRender = ipSettings.dontRenderLITE;

            mRKRToolStripMenuItem.Checked = !ipSettings.dontRenderMRKR;
            AssetMRKR.dontRender = ipSettings.dontRenderMRKR;

            mVPTToolStripMenuItem.Checked = !ipSettings.dontRenderMVPT;
            AssetMVPT.dontRender = ipSettings.dontRenderMVPT;

            pENDToolStripMenuItem.Checked = !ipSettings.dontRenderPEND;
            AssetPEND.dontRender = ipSettings.dontRenderPEND;

            pKUPToolStripMenuItem.Checked = !ipSettings.dontRenderPKUP;
            AssetPKUP.dontRender = ipSettings.dontRenderPKUP;

            pLATToolStripMenuItem.Checked = !ipSettings.dontRenderPLAT;
            AssetPLAT.dontRender = ipSettings.dontRenderPLAT;

            pLYRToolStripMenuItem.Checked = !ipSettings.dontRenderPLYR;
            AssetPLYR.dontRender = ipSettings.dontRenderPLYR;

            sFXToolStripMenuItem.Checked = !ipSettings.dontRenderSFX;
            AssetSFX.dontRender = ipSettings.dontRenderSFX;

            sIMPToolStripMenuItem.Checked = !ipSettings.dontRenderSIMP;
            AssetSIMP.dontRender = ipSettings.dontRenderSIMP;

            tRIGToolStripMenuItem.Checked = !ipSettings.dontRenderTRIG;
            AssetTRIG.dontRender = ipSettings.dontRenderTRIG;

            uIToolStripMenuItem.Checked = !ipSettings.dontRenderUI;
            AssetUI.dontRender = ipSettings.dontRenderUI;

            uIFTToolStripMenuItem.Checked = !ipSettings.dontRenderUIFT;
            AssetUIFT.dontRender = ipSettings.dontRenderUIFT;

            vILToolStripMenuItem.Checked = !ipSettings.dontRenderVIL;
            AssetVIL.dontRender = ipSettings.dontRenderVIL;

            templatesPersistentShiniesToolStripMenuItem.Checked = ipSettings.persistentShinies;
            ArchiveEditorFunctions.persistentShinies = ipSettings.persistentShinies;
            hideHelpInAssetDataEditorsToolStripMenuItem.Checked = ipSettings.hideHelp;
        }

        public void SetToolStripStatusLabel(string Text)
        {
            toolStripStatusLabel1.Text = Text;
        }

        private void SetProjectToolStripStatusLabel()
        {
            toolStripStatusLabelProject.Text = "Project: " + (currentProjectPath ?? "None");
        }

        public SharpRenderer renderer;

        private bool mouseMode = false;
        private System.Drawing.Point MouseCenter = new System.Drawing.Point();
        private MouseEventArgs oldMousePosition = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0);
        
        private void MouseMoveControl(object sender, MouseEventArgs e)
        {
            if (renderer.isDrawingUI)
            {
                float x = ((e.X - renderPanel.ClientRectangle.X) * 640f / renderPanel.ClientRectangle.Width);
                float y = ((e.Y - renderPanel.ClientRectangle.Y) * 480f / renderPanel.ClientRectangle.Height);

                SetToolStripStatusLabel(string.Format("Position: [{0:0.0000}, {1:0.0000}]", x, y) + " FPS: " + $"{renderer.sharpFPS.FPS:0.0000}");
            }
            else
            {
                if (mouseMode)
                {
                    renderer.Camera.AddYaw(MathUtil.DegreesToRadians(Cursor.Position.X - MouseCenter.X) / 4);
                    renderer.Camera.AddPitch(MathUtil.DegreesToRadians(Cursor.Position.Y - MouseCenter.Y) / 4);

                    Cursor.Position = MouseCenter;
                }
                else
                {
                    int deltaX = e.X - oldMousePosition.X;
                    int deltaY = e.Y - oldMousePosition.Y;

                    if (e.Button == MouseButtons.Middle)
                    {
                        renderer.Camera.AddYaw(MathUtil.DegreesToRadians(e.X - oldMousePosition.X));
                        renderer.Camera.AddPitch(MathUtil.DegreesToRadians(e.Y - oldMousePosition.Y));
                    }
                    if (e.Button == MouseButtons.Right && PressedKeys.Contains(Keys.ControlKey))
                    {
                        renderer.Camera.AddPositionSideways(e.X - oldMousePosition.X);
                        renderer.Camera.AddPositionUp(e.Y - oldMousePosition.Y);
                    }

                    foreach (ArchiveEditor ae in archiveEditors)
                    {
                        ae.MouseMoveGeneric(renderer.viewProjection, deltaX, deltaY, PressedKeys.Contains(Keys.T));
                    }
                }

                if (e.Delta != 0)
                    renderer.Camera.AddPositionForward(e.Delta / 24);
            }

            oldMousePosition = e;
        }

        private void ResetMouseCenter(object sender, EventArgs e)
        {
            MouseCenter = renderPanel.PointToScreen(new System.Drawing.Point(renderPanel.Width / 2, renderPanel.Height / 2));
        }

        private HashSet<Keys> PressedKeys = new HashSet<Keys>();

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!PressedKeys.Contains(e.KeyCode))
                PressedKeys.Add(e.KeyCode);

            switch (e.KeyCode) {
                case Keys.Q:
                    renderer.Camera.IncreaseCameraSpeed(-1);
                    break;
                case Keys.E:
                    renderer.Camera.IncreaseCameraSpeed(1);
                    break;
                case Keys.U:
                    uIModeToolStripMenuItem_Click(null, null);
                    break;
                case Keys.F:
                    ToggleWireFrame();
                    break;
                case Keys.G:
                    OpenInternalEditors();
                    break;
                case Keys.Z:
                    mouseMode = !mouseMode;
                    break;
                case Keys.C:
                    ToggleCulling(); 
                    break;
                case Keys.V:
                    ToggleGizmoType();
                    break;
                case Keys.Delete:
                    DeleteSelectedAssets();
                    break;
                case Keys.F1:
                    Program.ViewConfig.Show();
                    break;
                case Keys.F4:
                    saveAllOpenHIPsToolStripMenuItem_Click(sender, e);
                    break;
                case Keys.F5:
                    TryToRunGame();
                    break;
                case Keys.D1:
                    renderer.Camera.IncreaseCameraRotationSpeed(-1);
                    break;
                case Keys.D3:
                    renderer.Camera.IncreaseCameraRotationSpeed(1);
                    break;
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            PressedKeys.Remove(e.KeyCode);
        }

        private void MainForm_Deactivate(object sender, EventArgs e)
        {
            PressedKeys.Clear();
        }

        public void KeyboardController()
        {
            if (renderer.isDrawingUI)
                return;

            if (PressedKeys.Contains(Keys.W)) 
            { 
                if (PressedKeys.Contains(Keys.ControlKey))
                    renderer.Camera.AddPitch(-0.05f);
                else if (PressedKeys.Contains(Keys.ShiftKey))
                    renderer.Camera.AddPositionUp(0.25f);
                else 
                    renderer.Camera.AddPositionForward(0.25f);
            }

            if (PressedKeys.Contains(Keys.A)) 
            { 
                if (PressedKeys.Contains(Keys.ControlKey))
                    renderer.Camera.AddYaw(-0.05f);
                else 
                    renderer.Camera.AddPositionSideways(0.25f);
            }

            if (PressedKeys.Contains(Keys.S)) 
            { 
                if (PressedKeys.Contains(Keys.ControlKey))
                    renderer.Camera.AddPitch(0.05f);
                else if (PressedKeys.Contains(Keys.ShiftKey))
                    renderer.Camera.AddPositionUp(-0.25f);
                else
                    renderer.Camera.AddPositionForward(-0.25f);
            }

            if (PressedKeys.Contains(Keys.D)) 
            { 
                if (PressedKeys.Contains(Keys.ControlKey))
                    renderer.Camera.AddYaw(0.05f);
                else
                    renderer.Camera.AddPositionSideways(-0.25f);
            }

            if (PressedKeys.Contains(Keys.R))
                renderer.Camera.Reset();
        }

        public static bool alternateNamingMode = false;
        public List<ArchiveEditor> archiveEditors = new List<ArchiveEditor>();

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddArchiveEditor();
        }

        private void AddArchiveEditor(string filePath = null, HipHopFile.Platform scoobyPlatform = HipHopFile.Platform.Unknown)
        {
            ArchiveEditor temp = new ArchiveEditor(filePath, scoobyPlatform);
            archiveEditors.Add(temp);
            temp.Show();

            ToolStripMenuItem tempMenuItem = new ToolStripMenuItem(Path.GetFileName(temp.GetCurrentlyOpenFileName()));
            tempMenuItem.Click += new EventHandler(ToolStripClick);

            archiveEditorToolStripMenuItem.DropDownItems.Add(tempMenuItem);
        }

        public void ToolStripClick(object sender, EventArgs e)
        {
            archiveEditors[archiveEditorToolStripMenuItem.DropDownItems.IndexOf(sender as ToolStripItem) - 2].Show();
        }

        public void SetToolStripItemName(ArchiveEditor sender, string newName)
        {
            archiveEditorToolStripMenuItem.DropDownItems[archiveEditors.IndexOf(sender) + 2].Text = newName;
        }

        public void CloseArchiveEditor(ArchiveEditor sender)
        {
            int index = archiveEditors.IndexOf(sender);
            archiveEditorToolStripMenuItem.DropDownItems.RemoveAt(index + 2);
            archiveEditors.RemoveAt(index);
        }

        public void DisposeAllArchiveEditors()
        {
            foreach (ArchiveEditor ae in archiveEditors)
                ae.DisposeForClosing();
        }

        private bool UnsavedChanges()
        {
            foreach (ArchiveEditor ae in archiveEditors)
                if (ae.archive.UnsavedChanges)
                    return true;

            return false;
        }

        private void SaveAllChanges()
        {
            foreach (ArchiveEditor ae in archiveEditors)
                if (ae.archive.UnsavedChanges)
                    ae.Save();
        }

        private void noCullingCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleCulling();
        }

        public void ToggleCulling()
        {
            noCullingCToolStripMenuItem.Checked = !noCullingCToolStripMenuItem.Checked;
            renderer.device.SetNormalCullMode(noCullingCToolStripMenuItem.Checked ? CullMode.None : CullMode.Back);
        }

        private void wireframeFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleWireFrame();
        }

        public void ToggleWireFrame()
        {
            wireframeFToolStripMenuItem.Checked = !wireframeFToolStripMenuItem.Checked;
            renderer.device.SetNormalFillMode(wireframeFToolStripMenuItem.Checked ? FillMode.Wireframe : FillMode.Solid);
        }

        private void backgroundColorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(BitConverter.ToInt32(BitConverter.GetBytes(renderer.backgroundColor.ToBgra()).Reverse().ToArray(), 0))
            };
            if (colorDialog.ShowDialog() == DialogResult.OK)
                renderer.backgroundColor = new Color(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B, colorDialog.Color.A);
        }

        private void widgetColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
                renderer.SetWidgetColor(colorDialog.Color);
        }

        private void selectionColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
                renderer.SetSelectionColor(colorDialog.Color);
        }

        private void mVPTColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
                renderer.SetMvptColor(colorDialog.Color);
        }

        private void tRIGColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
                renderer.SetMvptColor(colorDialog.Color);
        }

        private void sFXInColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
                renderer.SetSfxColor(colorDialog.Color);
        }

        private void resetColorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            renderer.ResetColors();
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {
            Program.ViewConfig.Show();
        }

        private void viewConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.ViewConfig.Show();
        }

        private Rectangle ViewRectangle()
        {
            return new Rectangle(
                renderPanel.ClientRectangle.X,
                renderPanel.ClientRectangle.Y,
                renderPanel.ClientRectangle.Width,
                renderPanel.ClientRectangle.Height);
        }

        private void renderPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ScreenClicked(ViewRectangle(), e.X, e.Y);
            }
            else if (e.Button == MouseButtons.Right && PressedKeys.Contains(Keys.ShiftKey))
            {
                Vector3 Position = GetScreenClickedPosition(ViewRectangle(), e.X, e.Y);

                foreach (ArchiveEditor archiveEditor in archiveEditors)
                    if (archiveEditor.TemplateFocus)
                        archiveEditor.PlaceTemplate(Position);
            }
            else if (e.Button == MouseButtons.Right)
            {
                contextMenuStripMain.Show(renderPanel.PointToScreen(e.Location));
            }
        }

        private void renderPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                ScreenClicked(ViewRectangle(), e.X, e.Y, true);
        }

        public void ScreenClicked(Rectangle viewRectangle, int X, int Y, bool isMouseDown = false)
        {
            if (ArchiveEditorFunctions.FinishedMovingGizmo)
                ArchiveEditorFunctions.FinishedMovingGizmo = false;
            else
            {
                Ray ray = Ray.GetPickRay(X, Y, new Viewport(viewRectangle), renderer.viewProjection);

                if (isMouseDown)
                    ArchiveEditorFunctions.GizmoSelect(ray);
                else
                    SetSelectedIndex(renderer.isDrawingUI ?
                        ArchiveEditorFunctions.GetClickedAssetID2D(ray, renderer.Camera.FarPlane) :
                        ArchiveEditorFunctions.GetClickedAssetID(ray));
            }
        }

        public Vector3 GetScreenClickedPosition(Rectangle viewRectangle, int X, int Y)
        {
            Ray ray = Ray.GetPickRay(X, Y, new Viewport(viewRectangle), renderer.viewProjection);
            return ArchiveEditorFunctions.GetRayInterserctionPosition(ray);
        }

        private void renderPanel_MouseUp(object sender, MouseEventArgs e)
        {
            ArchiveEditorFunctions.ScreenUnclicked();
        }

        private void renderPanel_MouseLeave(object sender, EventArgs e)
        {
            ArchiveEditorFunctions.ScreenUnclicked();
        }

        public void SetSelectedIndex(uint assetID)
        {
            foreach (ArchiveEditor ae in archiveEditors)
                ae.SetSelectedIndices(new List<uint>() { assetID }, false, PressedKeys.Contains(Keys.ControlKey) || PressedKeys.Contains(Keys.Control));
        }

        private void OpenInternalEditors()
        {
            foreach (ArchiveEditor ae in archiveEditors)
                ae.OpenInternalEditors();
        }

        private void DeleteSelectedAssets()
        {
            foreach (ArchiveEditor ae in archiveEditors)
                ae.DeleteSelectedAssets();
        }

        private void addTextureFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog openFile = new CommonOpenFileDialog() { IsFolderPicker = true, Multiselect = true };
            if (openFile.ShowDialog() == CommonFileDialogResult.Ok)
                TextureManager.LoadTexturesFromFolder(openFile.FileNames);
        }

        private void addTXDArchiveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog() { Filter = "TXD files|*.txd" };
            if (openFile.ShowDialog() == DialogResult.OK)
                TextureManager.LoadTexturesFromTXD(openFile.FileName);
        }

        private void clearTexturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TextureManager.ClearTextures();
        }

        private void pLATPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pLATPreviewToolStripMenuItem.Checked = !pLATPreviewToolStripMenuItem.Checked;
            PlaceableAsset.movementPreview = pLATPreviewToolStripMenuItem.Checked;

            ResetMovementPreview();
        }

        public void ResetMovementPreview()
        {
            foreach (ArchiveEditor ae in archiveEditors)
                foreach (Asset a in ae.archive.GetAllAssets())
                    if (a is PlaceableAsset p)
                        p.Reset();
        }

        private void useMaxRenderDistanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            useMaxRenderDistanceToolStripMenuItem.Checked = !useMaxRenderDistanceToolStripMenuItem.Checked;
            AssetMODL.renderBasedOnLodt = useMaxRenderDistanceToolStripMenuItem.Checked;
        }

        private void UsePIPTForRenderingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            usePIPTForRenderingToolStripMenuItem.Checked = !usePIPTForRenderingToolStripMenuItem.Checked;
            AssetMODL.renderBasedOnPipt = usePIPTForRenderingToolStripMenuItem.Checked;
        }

        private void useLegacyAssetIDFormatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            useLegacyAssetIDFormatToolStripMenuItem.Checked = !useLegacyAssetIDFormatToolStripMenuItem.Checked;
            AssetIDTypeConverter.Legacy = useLegacyAssetIDFormatToolStripMenuItem.Checked;
        }

        private void assetNameAssetIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            assetIDAssetNameToolStripMenuItem.Checked = false;
            assetNameAssetIDToolStripMenuItem.Checked = true;
            alternateNamingMode = false;
        }

        private void levelModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            levelModelToolStripMenuItem.Checked = !levelModelToolStripMenuItem.Checked;
            AssetJSP.dontRender = !levelModelToolStripMenuItem.Checked;
        }

        private void bUTNToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bUTNToolStripMenuItem.Checked = !bUTNToolStripMenuItem.Checked;
            AssetBUTN.dontRender = !bUTNToolStripMenuItem.Checked;
        }

        private void bOULToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bOULToolStripMenuItem.Checked = !bOULToolStripMenuItem.Checked;
            AssetBOUL.dontRender = !bOULToolStripMenuItem.Checked;
        }

        private void cAMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cAMToolStripMenuItem.Checked = !cAMToolStripMenuItem.Checked;
            AssetCAM.dontRender = !cAMToolStripMenuItem.Checked;
        }

        private void mVPTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mVPTToolStripMenuItem.Checked = !mVPTToolStripMenuItem.Checked;
            AssetMVPT_Scooby.dontRender = !mVPTToolStripMenuItem.Checked;
        }

        private void pKUPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pKUPToolStripMenuItem.Checked = !pKUPToolStripMenuItem.Checked;
            AssetPKUP.dontRender = !pKUPToolStripMenuItem.Checked;
        }

        private void dSTRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dSTRToolStripMenuItem.Checked = !dSTRToolStripMenuItem.Checked;
            AssetDSTR_Scooby.dontRender = !dSTRToolStripMenuItem.Checked;
        }

        private void tRIGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tRIGToolStripMenuItem.Checked = !tRIGToolStripMenuItem.Checked;
            AssetTRIG.dontRender = !tRIGToolStripMenuItem.Checked;
        }

        private void pLATToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pLATToolStripMenuItem.Checked = !pLATToolStripMenuItem.Checked;
            AssetPLAT.dontRender = !pLATToolStripMenuItem.Checked;
        }

        private void sIMPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            sIMPToolStripMenuItem.Checked = !sIMPToolStripMenuItem.Checked;
            AssetSIMP.dontRender = !sIMPToolStripMenuItem.Checked;
        }

        private void vILToolStripMenuItem_Click(object sender, EventArgs e)
        {
            vILToolStripMenuItem.Checked = !vILToolStripMenuItem.Checked;
            AssetVIL.dontRender = !vILToolStripMenuItem.Checked;
        }

        private void mRKRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mRKRToolStripMenuItem.Checked = !mRKRToolStripMenuItem.Checked;
            AssetMRKR.dontRender = !mRKRToolStripMenuItem.Checked;
        }

        private void pLYRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pLYRToolStripMenuItem.Checked = !pLYRToolStripMenuItem.Checked;
            AssetPLYR.dontRender = !pLYRToolStripMenuItem.Checked;
        }

        private void sFXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            sFXToolStripMenuItem.Checked = !sFXToolStripMenuItem.Checked;
            AssetSFX.dontRender = !sFXToolStripMenuItem.Checked;
        }

        private void dYNAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dYNAToolStripMenuItem.Checked = !dYNAToolStripMenuItem.Checked;
            AssetDYNA.dontRender = !dYNAToolStripMenuItem.Checked;
        }

        private void uIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uIToolStripMenuItem.Checked = !uIToolStripMenuItem.Checked;
            AssetUI.dontRender = !uIToolStripMenuItem.Checked;
        }

        private void uIFTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uIFTToolStripMenuItem.Checked = !uIFTToolStripMenuItem.Checked;
            AssetUIFT.dontRender = !uIFTToolStripMenuItem.Checked;
        }

        private void eGENToolStripMenuItem_Click(object sender, EventArgs e)
        {
            eGENToolStripMenuItem.Checked = !eGENToolStripMenuItem.Checked;
            AssetEGEN.dontRender = !eGENToolStripMenuItem.Checked;
        }

        private void hANGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hANGToolStripMenuItem.Checked = !hANGToolStripMenuItem.Checked;
            AssetHANG.dontRender = !hANGToolStripMenuItem.Checked;
        }

        private void pENDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pENDToolStripMenuItem.Checked = !pENDToolStripMenuItem.Checked;
            AssetPEND.dontRender = !pENDToolStripMenuItem.Checked;
        }

        private void lITEToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lITEToolStripMenuItem.Checked = !lITEToolStripMenuItem.Checked;
            AssetLITE.dontRender = !lITEToolStripMenuItem.Checked;
        }

        private void uIModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uIModeToolStripMenuItem.Checked = !uIModeToolStripMenuItem.Checked;
            renderer.isDrawingUI = uIModeToolStripMenuItem.Checked;

            if (renderer.isDrawingUI)
            {
                if (AssetUI.dontRender)
                    uIToolStripMenuItem_Click(null, null);
                if (AssetUIFT.dontRender)
                    uIFTToolStripMenuItem_Click(null, null);
            }

            renderer.Camera.Reset();
            mouseMode = false;
        }
        
        private void HideHelpInAssetDataEditorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ArchiveEditorFunctions.hideHelp = !ArchiveEditorFunctions.hideHelp;
            hideHelpInAssetDataEditorsToolStripMenuItem.Checked = ArchiveEditorFunctions.hideHelp;
            foreach (ArchiveEditor ae in archiveEditors)
                ae.SetHideHelp(ArchiveEditorFunctions.hideHelp);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.AboutBox.Show();
        }

        public string GetAssetNameFromID(uint assetID)
        {
            foreach (ArchiveEditor archiveEditor in archiveEditors)
                if (archiveEditor.HasAsset(assetID))
                    return archiveEditor.GetAssetNameFromID(assetID);

            if (ArchiveEditorFunctions.nameDictionary.ContainsKey(assetID))
                return ArchiveEditorFunctions.nameDictionary[assetID];

            return "0x" + assetID.ToString("X8");
        }

        public bool AssetExists(uint assetID)
        {
            foreach (ArchiveEditor archiveEditor in archiveEditors)
                if (archiveEditor.HasAsset(assetID))
                    return true;

            return ArchiveEditorFunctions.nameDictionary.ContainsKey(assetID);
        }

        public void FindWhoTargets(uint assetID)
        {
            List<uint> whoTargets = WhoTargets(assetID);

            bool willOpen = true;
            if (whoTargets.Count > 15)
            {
                TopMost = true;
                willOpen = MessageBox.Show(
                    $"Warning: you're going to open {whoTargets.Count} Asset Data Editor windows. Are you sure you want to do that?", 
                    "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning
                ) == DialogResult.Yes;
                TopMost = false;
            }

            if (willOpen)
                foreach (ArchiveEditor archiveEditor in archiveEditors)
                    archiveEditor.archive.OpenInternalEditor(whoTargets, true);
        }

        public List<uint> WhoTargets(uint assetID)
        {
            List<uint> whoTargets = new List<uint>();
            foreach (ArchiveEditor archiveEditor in archiveEditors)
                whoTargets.AddRange(archiveEditor.archive.FindWhoTargets(assetID));

            return whoTargets;
        }

        public void ClearTemplateFocus()
        {
            foreach (ArchiveEditor archiveEditor in archiveEditors)
                archiveEditor.TemplateFocusOff();
        }

        private void assetIDAssetNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            assetIDAssetNameToolStripMenuItem.Checked = true;
            assetNameAssetIDToolStripMenuItem.Checked = false;
            alternateNamingMode = true;
        }

        private void uIModeAutoSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Width = (int)(Height * 656f / 565f);
        }

        private void ensureAssociationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Will set Industrial Park as default application for HIP and HOP file formats on registry.", 
                "Associate HIP/HOP files", MessageBoxButtons.OKCancel
            );
            if (result == DialogResult.OK)
                FileAssociations.FileAssociations.EnsureAssociationsSet();
        }

        private void UnselectTemplateButtonRecursive(ToolStripItem t)
        {
            if (t is ToolStripMenuItem toolStripMenuItem)
            {
                if (toolStripMenuItem.HasDropDownItems)
                    foreach (ToolStripItem ti in toolStripMenuItem.DropDownItems)
                        UnselectTemplateButtonRecursive(ti);
                else
                    toolStripMenuItem.Checked = false;
            }
        }

        private void TemplateToolStripItemClick(object sender, EventArgs e)
        {
            UnselectTemplateButtonRecursive(toolStripMenuItem_Templates);

            string text = ((ToolStripItem)sender).Text;
            foreach (AssetTemplate template in Enum.GetValues(typeof(AssetTemplate)))
            {
                if (text == template.ToString())
                {
                    ArchiveEditorFunctions.CurrentAssetTemplate = template;
                    toolStripStatusLabelTemplate.Text = "Template: " + ArchiveEditorFunctions.CurrentAssetTemplate.ToString();
                    toolStripComboBoxUserTemplate.SelectedItem = null;
                    ((ToolStripMenuItem)sender).Checked = true;
                    return;
                }
            }

            TopMost = true;
            MessageBox.Show("There was a problem setting your template for placement");
            TopMost = false;
        }

        private void userTemplateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateUserTemplateComboBox();
        }

        public void UpdateUserTemplateComboBox()
        {
            toolStripComboBoxUserTemplate.Items.Clear();

            foreach (string s in Directory.GetFiles(userTemplatesFolder))
            {
                toolStripComboBoxUserTemplate.Items.Add(Path.GetFileName(s));

                if (toolStripComboBoxUserTemplate.Size.Width < 8 * Path.GetFileName(s).Length)
                    toolStripComboBoxUserTemplate.Size = new System.Drawing.Size(8 * Path.GetFileName(s).Length, toolStripComboBoxUserTemplate.Size.Height);
            }
        }

        private void toolStripComboBoxUserTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (toolStripComboBoxUserTemplate.SelectedIndex != -1)
            {
                ArchiveEditorFunctions.CurrentAssetTemplate = AssetTemplate.UserTemplate;
                ArchiveEditorFunctions.CurrentUserTemplate = toolStripComboBoxUserTemplate.SelectedItem.ToString();
                toolStripStatusLabelTemplate.Text = $"Template: {toolStripComboBoxUserTemplate.SelectedItem.ToString()} (User)";
            }
        }

        private void manageUserTemplatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.UserTemplateManager.Show();
        }

        private void templatesPersistentShiniesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            templatesPersistentShiniesToolStripMenuItem.Checked = !templatesPersistentShiniesToolStripMenuItem.Checked;
            ArchiveEditorFunctions.persistentShinies = templatesPersistentShiniesToolStripMenuItem.Checked;
        }

        private void ToggleGizmoType(GizmoMode mode = GizmoMode.Null)
        {
            GizmoMode outMode = ArchiveEditorFunctions.ToggleGizmoType(mode);

            positionToolStripMenuItem.Checked = false;
            rotationToolStripMenuItem.Checked = false;
            scaleToolStripMenuItem.Checked = false;
            positionLocalToolStripMenuItem.Checked = false;
            switch (outMode) {
                case GizmoMode.Position:
                    positionToolStripMenuItem.Checked = true;
                    break;
                case GizmoMode.Rotation:
                    rotationToolStripMenuItem.Checked = true;
                    break;
                case GizmoMode.Scale:
                    scaleToolStripMenuItem.Checked = true;
                    break;
                case GizmoMode.PositionLocal:
                    positionLocalToolStripMenuItem.Checked = true;
                    break;
            }
        }

        private void positionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleGizmoType(GizmoMode.Position);
        }

        private void rotationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleGizmoType(GizmoMode.Rotation);
        }

        private void scaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleGizmoType(GizmoMode.Scale);
        }

        private void positionLocalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleGizmoType(GizmoMode.PositionLocal);
        }

        private void eventSearchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.EventSearch.Show();
        }
        
        private void assetIDGeneratorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.AssetIDGenerator.Show();
        }
        
        private void MainForm_Resize(object sender, EventArgs e)
        {
             ArchiveEditorFunctions.allowRender = WindowState != FormWindowState.Minimized;
             //SetAllTopMost(WindowState != FormWindowState.Minimized);
        }

        private bool allTopMost = false;

        private void SetAllTopMost(bool value)
        {
            Program.AboutBox.TopMost = value;
            Program.ViewConfig.TopMost = value;
            Program.UserTemplateManager.TopMost = value;

            Program.EventSearch.TopMost = value;
            Program.AssetIDGenerator.TopMost = value;

            foreach (ArchiveEditor ae in archiveEditors)
                ae.SetAllTopMost(value);

            allTopMost = value;
        }

        private void runGameF5ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TryToRunGame();
        }

        private void TryToRunGame()
        {
            string dolPath = string.Empty;
            foreach (var ae in archiveEditors) 
            {
                string hipName = ae.GetCurrentlyOpenFileName().ToLower();
                string rootFolderName = Path.GetDirectoryName(Path.GetDirectoryName(hipName));

                if (!(hipName.Contains("boot") || hipName.Contains("font") || hipName.Contains("plat")))
                    rootFolderName = Path.GetDirectoryName(rootFolderName);

                dolPath = Path.Combine(rootFolderName, "sys", "main.dol");

                if (File.Exists(dolPath))
                    break;

                dolPath = string.Empty;
            }

            if (dolPath == string.Empty)
                MessageBox.Show("Unable to find DOL to launch.");
            else RemoteControl.TryToRunGame(dolPath);
        }

        private void saveAllOpenHIPsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var ae in archiveEditors)
                if (ae.archive.UnsavedChanges)
                    ae.Save();
        }
    }
}