using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32;
using Netch.Controllers;
using Netch.Enums;
using Netch.Forms.ModeForms;
using Netch.Interfaces;
using Netch.Models;
using Netch.Properties;
using Netch.Services;
using Netch.Utils;
using Serilog;

namespace Netch.Forms
{
    public partial class MainForm : Form
    {
        #region Start

        private readonly Dictionary<string, object> _mainFormText = new();

        private bool _textRecorded;

        public MainForm()
        {
            InitializeComponent();
            var appUpdate = new ToolStripMenuItem("Check for Application Updates") { Name = "AppUpdateMenuItem" };
            appUpdate.Click += CheckForAppUpdates_Click;
            HelpToolStripMenuItem.DropDownItems.Insert(0, appUpdate);
            NotifyIcon.Icon = Icon = Resources.icon;

            AddAddServerToolStripMenuItems();
            #region i18N Translations

            if (Flags.NoSupport)
                _mainFormText.Add(Name, new[] { "{0} ({1})", "Netch", "No Support" });

            _mainFormText.Add(UninstallServiceToolStripMenuItem.Name, new[] { "Uninstall {0}", "NF Service" });

            #endregion

            // 监听电源事件
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        }

        private void AddAddServerToolStripMenuItems()
        {
            foreach (var serversUtil in ServerHelper.ServerUtilDictionary.Values.OrderBy(i => i.Priority)
                .Where(i => !string.IsNullOrEmpty(i.FullName)))
            {
                var fullName = serversUtil.FullName;
                var control = new ToolStripMenuItem
                {
                    Name = $"Add{fullName}ServerToolStripMenuItem",
                    Size = new Size(259, 22),
                    Text = i18N.TranslateFormat("Add [{0}] Server", fullName),
                    Tag = serversUtil
                };

                _mainFormText.Add(control.Name, new[] { "Add [{0}] Server", fullName });
                control.Click += AddServerToolStripMenuItem_Click;
                ServerToolStripMenuItem.DropDownItems.Add(control);
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // 计算 ComboBox绘制 目标宽度
            RecordSize();

            LoadServers();
            SelectLastServer();
            DelayTestHelper.UpdateTick(true);

            ModeHelper.InitWatcher();
            ModeHelper.Load();
            LoadModes();

            // 加载翻译
            TranslateControls();

            // 隐藏 ConnectivityStatusLabel
            ConnectivityStatusVisible(false);

            // 加载快速配置
            LoadProfiles();

            // 检查更新
            if (Global.Settings.CheckUpdateWhenOpened)
                CheckCoreUpdatesAsync(interactive: false).Forget();

            Netch.SingleInstance.ListenForArgumentsFromSuccessiveInstances();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Global.Settings.StartWhenOpened && IsWaiting() &&
                ServerComboBox.SelectedItem is Server && ModeComboBox.SelectedItem is Mode)
                await StartCoreAsync();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            _numberBoxWidth = ServerComboBox.Width / 10;
            _numberBoxX = _numberBoxWidth * 9;
            _numberBoxWrap = _numberBoxWidth / 30;
            ServerComboBox.Invalidate();
        }

        private void RecordSize()
        {
            _numberBoxWidth = ServerComboBox.Width / 10;
            _numberBoxX = _numberBoxWidth * 9;
            _numberBoxWrap = _numberBoxWidth / 30;

            _configurationGroupBoxHeight = ConfigurationGroupBox.Height;
            _profileConfigurationHeight = ConfigurationGroupBox.Controls[0].Height / 3; // 因为 AutoSize, 所以得到的是Controls的总高度
            _profileGroupBoxPaddingHeight = ProfileGroupBox.Height - ProfileTable.Height;
            _profileTableHeight = ProfileTable.Height;
        }

        private void TranslateControls()
        {
            #region Record English

            if (!_textRecorded)
            {
                void RecordText(Component component)
                {
                    try
                    {
                        switch (component)
                        {
                            case TextBoxBase:
                            case ListControl:
                                break;
                            case Control c:
                                _mainFormText.Add(c.Name, c.Text);
                                break;
                            case ToolStripItem c:
                                _mainFormText.Add(c.Name, c.Text);
                                break;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // ignored
                    }
                }

                Utils.Utils.ComponentIterator(this, RecordText);
                Utils.Utils.ComponentIterator(NotifyMenu, RecordText);
                _textRecorded = true;
            }

            #endregion

            #region Translate

            void TranslateText(Component component)
            {
                switch (component)
                {
                    case TextBoxBase:
                    case ListControl:
                        break;
                    case Control c:
                        if (_mainFormText.ContainsKey(c.Name))
                            c.Text = ControlText(c.Name);

                        break;
                    case ToolStripItem c:
                        if (_mainFormText.ContainsKey(c.Name))
                            c.Text = ControlText(c.Name);

                        break;
                }

                string ControlText(string name)
                {
                    var value = _mainFormText[name];
                    if (value.Equals(string.Empty))
                        return string.Empty;

                    if (value is object[] values)
                        return i18N.TranslateFormat((string)values.First(), values.Skip(1).ToArray());

                    return i18N.Translate(value);
                }
            }

            Utils.Utils.ComponentIterator(this, TranslateText);
            Utils.Utils.ComponentIterator(NotifyMenu, TranslateText);

            #endregion

            UsedBandwidthLabel.Text = $@"{i18N.Translate("Used", ": ")}0 KB";
            State = State;
            VersionLabel.Text = UpdateChecker.Version;
        }

        #endregion

        #region Controls

        #region MenuStrip

        #region Server

        private async void ImportServersFromClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var texts = Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(texts))
            {
                var servers = ShareLink.ParseText(texts);
                Global.Settings.Server.AddRange(servers);
                StatusText(i18N.TranslateFormat("Import {0} server(s) form Clipboard", servers.Count));

                LoadServers();
                await Configuration.SaveAsync();
            }
        }

        private async void AddServerToolStripMenuItem_Click([NotNull] object? sender, EventArgs? e)
        {
            if (sender == null)
                throw new ArgumentNullException(nameof(sender));

            var util = (IServerUtil)((ToolStripMenuItem)sender).Tag;

            var previousCount = Global.Settings.Server.Count;
            util.Create();

            if (Global.Settings.Server.Count == previousCount)
                return;

            Global.Settings.ServerComboBoxSelectedIndex = Global.Settings.Server.Count - 1;
            LoadServers();
            await Configuration.SaveAsync();
        }

        #endregion

        #region Mode

        private void CreateProcessModeToolStripButton_Click(object sender, EventArgs e)
        {
            Hide();
            new ProcessForm().ShowDialog();
            Show();
        }

        private void createRouteTableModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Hide();
            new RouteForm().ShowDialog();
            Show();
        }

        #endregion

        #region Options

        private async void CheckForAppUpdates_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem item)
                return;

            item.Enabled = false;
            try
            {
                var update = await UpdateChecker.CheckAsync();
                if (update == null)
                {
                    MessageBox.Show(this, i18N.Translate("No newer application release is available."), item.Text);
                    return;
                }

                var message = i18N.TranslateFormat("Netch {0} is available. Download and install it? Active proxy sessions will stop.", update.Version);
                if (MessageBox.Show(this, message, item.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                StatusText(i18N.Translate("Downloading application update…"));
                var stage = await Updater.StageAsync(update);
                await StopAsync();
                await Configuration.SaveAsync();
                Updater.Start(stage);
                Exit(forceExit: true, saveConfiguration: false);
            }
            catch (Exception error)
            {
                Log.Warning(error, "Application update failed");
                StatusText();
                MessageBox.Show(this, i18N.Translate("Application update failed.") + "\n" + error.Message,
                    item.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                item.Enabled = true;
            }
        }

        private async void CheckForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await CheckCoreUpdatesAsync(interactive: true);
        }

        private async Task CheckCoreUpdatesAsync(bool interactive)
        {
            CheckForUpdatesToolStripMenuItem.Enabled = false;
            try
            {
                var results = await Task.WhenAll(
                    CoreUpdates.CheckAsync("XTLS/Xray-core", "xray.exe"),
                    CoreUpdates.CheckAsync("SagerNet/sing-box", "sing-box.exe"));
                if (IsDisposed)
                    return;

                if (interactive)
                    MessageBox.Show(this, string.Join("\n\n", results.Select(result => result.Detail)), i18N.Translate("Proxy Core Updates"));
                else if (IsWaiting() && results.Any(result => result.UpdateAvailable))
                    StatusText(i18N.Translate("Proxy core updates are available. See the Help menu."));
            }
            finally
            {
                CheckForUpdatesToolStripMenuItem.Enabled = true;
            }
        }

        private void OpenDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Utils.Utils.Open(".\\");
        }

        private async void CleanDNSCacheToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    NativeMethods.RefreshDNSCache();
                    DnsUtils.ClearCache();
                });

                StatusText(i18N.Translate("DNS cache cleanup succeeded"));
            }
            catch (Exception)
            {
                StatusText();
            }
        }

        private async void UninstallServiceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Enabled = false;
            StatusText(i18N.TranslateFormat("Uninstalling {0}", "NF Service"));
            var uninstalled = false;
            try
            {
                uninstalled = await Task.Run(NFController.UninstallDriver);
            }
            finally
            {
                StatusText(uninstalled ? i18N.TranslateFormat("{0} has been uninstalled", "NF Service") : null);
                Enabled = true;
            }
        }

        private void RemoveNetchFirewallRulesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Firewall.RemoveNetchFwRules();
        }

        private void ShowHideConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var windowStyles = (WINDOW_STYLE)PInvoke.GetWindowLong(new HWND(Netch.ConsoleHwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            var visible = windowStyles.HasFlag(WINDOW_STYLE.WS_VISIBLE);
            PInvoke.ShowWindow(Netch.ConsoleHwnd, visible ? SHOW_WINDOW_CMD.SW_HIDE : SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }

        #endregion

        /// <summary>
        ///     菜单栏强制退出
        /// </summary>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Exit(true);
        }

        private void VersionLabel_Click(object sender, EventArgs e)
        {
            Utils.Utils.Open($"https://github.com/{UpdateChecker.Owner}/{UpdateChecker.Repo}/releases");
        }

        #endregion

        #region ControlButton

        private async void ControlButton_Click(object? sender, EventArgs? e)
        {
            if (!IsWaiting())
            {
                await StopCoreAsync();
                return;
            }

            await StartCoreAsync();
        }

        private async Task StartCoreAsync()
        {
            Configuration.SaveAsync().Forget();

            // 服务器、模式 需选择
            if (ServerComboBox.SelectedItem is not Server server)
            {
                MessageBoxX.Show(i18N.Translate("Please select a server first"));
                return;
            }

            if (ModeComboBox.SelectedItem is not Mode mode)
            {
                MessageBoxX.Show(i18N.Translate("Please select a mode first"));
                return;
            }

            State = State.Starting;

            try
            {
                await MainController.StartAsync(server, mode);
            }
            catch (Exception exception)
            {
                State = State.Stopped;
                StatusText(i18N.Translate("Start failed"));
                MessageBoxX.Show(exception.Message, LogLevel.ERROR);
                return;
            }

            State = State.Started;

            Task.Run(Bandwidth.NetTraffic).Forget();

            HttpStatusLabel.Text = i18N.Translate("Test proxy connection");
            HttpStatusLabel.Visible = true;
            NatTypeStatusLabel.Text = i18N.Translate("Test NAT type");
            NatTypeStatusLabel.Visible = mode.Type != ModeType.Process || Global.Settings.Redirector.FilterProtocol.HasFlag(PortType.UDP);

            if (Global.Settings.MinimizeWhenStarted)
                Minimize();

            if (Global.Settings.StartedPingInterval > 0 && DelayTestHelper.Range.InRange(Global.Settings.StartedPingInterval))
            {
                _startedPingCts = new CancellationTokenSource();
                PingStartedServerAsync(server, _startedPingCts).Forget();
            }
        }

        private CancellationTokenSource? _startedPingCts;

        private async Task PingStartedServerAsync(Server server, CancellationTokenSource source)
        {
            using (source)
            {
                try
                {
                    var interval = TimeSpan.FromSeconds(Global.Settings.StartedPingInterval);
                    while (!source.IsCancellationRequested)
                    {
                        await server.PingAsync(source.Token);
                        ServerComboBox.Refresh();
                        await Task.Delay(interval, source.Token);
                    }
                }
                catch (OperationCanceledException) when (source.IsCancellationRequested) { }
                finally
                {
                    if (_startedPingCts == source)
                        _startedPingCts = null;
                }
            }
        }

        #endregion

        #region SettingsButton

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            var oldSettings = Global.Settings.Clone();

            Hide();
            new SettingForm().ShowDialog();

            if (oldSettings.Language != Global.Settings.Language)
            {
                i18N.Load(Global.Settings.Language);
                TranslateControls();
                LoadModes();
                LoadProfiles();
            }

            if (oldSettings.DetectionTick != Global.Settings.DetectionTick)
                DelayTestHelper.UpdateTick(true);

            if (oldSettings.ProfileCount != Global.Settings.ProfileCount)
                LoadProfiles();

            Show();
        }

        #endregion

        #region Server

        private void LoadServers()
        {
            ServerComboBox.Items.Clear();
            ServerComboBox.Items.AddRange(Global.Settings.Server.Cast<object>().ToArray());
            SelectLastServer();
        }

        private void SelectLastServer()
        {
            // 如果值合法，选中该位置
            if (Global.Settings.ServerComboBoxSelectedIndex > 0 && Global.Settings.ServerComboBoxSelectedIndex < ServerComboBox.Items.Count)
                ServerComboBox.SelectedIndex = Global.Settings.ServerComboBoxSelectedIndex;
            // 如果值非法，且当前 ServerComboBox 中有元素，选择第一个位置
            else if (ServerComboBox.Items.Count > 0)
                ServerComboBox.SelectedIndex = 0;

            // 如果当前 ServerComboBox 中没元素，不做处理
        }

        private void ServerComboBox_SelectionChangeCommitted(object sender, EventArgs o)
        {
            Global.Settings.ServerComboBoxSelectedIndex = ServerComboBox.SelectedIndex;
        }

        private async void EditServerPictureBox_Click(object sender, EventArgs e)
        {
            // 当前ServerComboBox中至少有一项
            if (!(ServerComboBox.SelectedItem is Server server))
            {
                MessageBoxX.Show(i18N.Translate("Please select a server first"));
                return;
            }

            ServerHelper.GetUtilByTypeName(server.Type).Edit(server);
            LoadServers();
            await Configuration.SaveAsync();
        }

        private async void SpeedPictureBox_Click(object sender, EventArgs e)
        {
            Enabled = false;
            try
            {
                StatusText(i18N.Translate("Testing server endpoint latency"));
                if (!IsWaiting() || ModifierKeys == Keys.Control)
                {
                    if (ServerComboBox.SelectedItem is Server server)
                        await server.PingAsync();
                }
                else
                {
                    await DelayTestHelper.PerformTestAsync(true);
                }
            }
            finally
            {
                ServerComboBox.Refresh();
                Enabled = true;
                StatusText();
            }
        }

        private void CopyLinkPictureBox_Click(object sender, EventArgs e)
        {
            // 当前ServerComboBox中至少有一项
            if (!(ServerComboBox.SelectedItem is Server server))
            {
                MessageBoxX.Show(i18N.Translate("Please select a server first"));
                return;
            }

            try
            {
                //听说巨硬BUG经常会炸，所以Catch一下 :D
                string text;
                if (ModifierKeys == Keys.Control)
                    text = ShareLink.GetNetchLink(server);
                else
                    text = ShareLink.GetShareLink(server);

                Clipboard.SetText(text);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void DeleteServerPictureBox_Click(object sender, EventArgs e)
        {
            // 当前 ServerComboBox 中至少有一项
            if (!(ServerComboBox.SelectedItem is Server server))
            {
                MessageBoxX.Show(i18N.Translate("Please select a server first"));
                return;
            }

            Global.Settings.Server.Remove(server);
            LoadServers();
        }

        #endregion

        #region Mode

        public void LoadModes()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(LoadModes));
                return;
            }

            ModeComboBox.Items.Clear();
            ModeComboBox.Items.AddRange(Global.Modes.Cast<object>().ToArray());
            ModeComboBox.Tag = null;
            SelectLastMode();
        }

        private void SelectLastMode()
        {
            var index = Global.Settings.ModeComboBoxSelectedIndex;
            ModeComboBox.SelectedIndex = index >= 0 && index < ModeComboBox.Items.Count ? index : -1;
        }

        private void ModeComboBox_SelectionChangeCommitted(object sender, EventArgs e)
        {
            Global.Settings.ModeComboBoxSelectedIndex = ModeComboBox.SelectedIndex;
        }

        private void EditModePictureBox_Click(object sender, EventArgs e)
        {
            // 当前ModeComboBox中至少有一项
            if (ModeComboBox.SelectedIndex == -1)
            {
                MessageBoxX.Show(i18N.Translate("Please select a mode first"));
                return;
            }

            var mode = (Mode)ModeComboBox.SelectedItem;
            if (ModifierKeys == Keys.Control)
            {
                Utils.Utils.Open(ModeHelper.GetFullPath(mode.RelativePath!));
                return;
            }

            switch (mode.Type)
            {
                case ModeType.Process:
                    Hide();
                    new ProcessForm(mode).ShowDialog();
                    Show();
                    break;
                case ModeType.ProxyRuleIPs:
                case ModeType.BypassRuleIPs:
                    Hide();
                    new RouteForm(mode).ShowDialog();
                    Show();
                    break;
                default:
                    Utils.Utils.Open(ModeHelper.GetFullPath(mode.RelativePath!));
                    break;
            }
        }

        private void DeleteModePictureBox_Click(object sender, EventArgs e)
        {
            // 当前ModeComboBox中至少有一项
            if (ModeComboBox.Items.Count <= 0 || ModeComboBox.SelectedIndex == -1)
            {
                MessageBoxX.Show(i18N.Translate("Please select a mode first"));
                return;
            }

            ModeHelper.Delete((Mode)ModeComboBox.SelectedItem);
            SelectLastMode();
        }

        #endregion

        #region Profile

        private int _configurationGroupBoxHeight;
        private int _profileConfigurationHeight;
        private int _profileGroupBoxPaddingHeight;
        private int _profileTableHeight;

        private void LoadProfiles()
        {
            // Clear
            foreach (var button in ProfileTable.Controls)
                ((Button)button).Dispose();

            ProfileTable.Controls.Clear();
            ProfileTable.ColumnStyles.Clear();
            ProfileTable.RowStyles.Clear();

            var profileCount = Global.Settings.ProfileCount;
            if (profileCount == 0)
            {
                // Hide Profile GroupBox, Change window size
                configLayoutPanel.RowStyles[2].SizeType = SizeType.Percent;
                configLayoutPanel.RowStyles[2].Height = 0;
                ProfileGroupBox.Visible = false;

                ConfigurationGroupBox.Height = _configurationGroupBoxHeight - _profileConfigurationHeight;
            }
            else
            {
                // Load Profiles

                if (Global.Settings.ProfileTableColumnCount == 0)
                    Global.Settings.ProfileTableColumnCount = 5;

                var columnCount = Global.Settings.ProfileTableColumnCount;

                ProfileTable.ColumnCount = profileCount >= columnCount ? columnCount : profileCount;
                ProfileTable.RowCount = (int)Math.Ceiling(profileCount / (float)columnCount);

                for (var i = 0; i < profileCount; ++i)
                {
                    var profile = Global.Settings.Profiles.SingleOrDefault(p => p.Index == i);
                    var b = new Button
                    {
                        Dock = DockStyle.Fill,
                        Text = profile?.ProfileName ?? i18N.Translate("None"),
                        Tag = profile
                    };

                    b.Click += ProfileButton_Click;
                    ProfileTable.Controls.Add(b, i % columnCount, i / columnCount);
                }

                // equal column
                for (var i = 1; i <= ProfileTable.RowCount; i++)
                    ProfileTable.RowStyles.Add(new RowStyle(SizeType.Percent, 1));

                for (var i = 1; i <= ProfileTable.ColumnCount; i++)
                    ProfileTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1));

                configLayoutPanel.RowStyles[2].SizeType = SizeType.AutoSize;
                ProfileGroupBox.Visible = true;
                ProfileGroupBox.Height = ProfileTable.RowCount * _profileTableHeight + _profileGroupBoxPaddingHeight;
                ConfigurationGroupBox.Height = _configurationGroupBoxHeight;
            }
        }

        private void ActiveProfile(Profile profile)
        {
            ProfileNameText.Text = profile.ProfileName;

            var server = ServerComboBox.Items.Cast<Server>().FirstOrDefault(s => s.Remark.Equals(profile.ServerRemark));
            var mode = ModeComboBox.Items.Cast<Mode>().FirstOrDefault(m => m.Remark.Equals(profile.ModeRemark));

            if (server == null)
                throw new Exception("Server not found.");

            if (mode == null)
                throw new Exception("Mode not found.");

            ServerComboBox.SelectedItem = server;
            ModeComboBox.SelectedItem = mode;
        }

        private Profile CreateProfileAtIndex(int index)
        {
            var server = (Server)ServerComboBox.SelectedItem;
            var mode = (Mode)ModeComboBox.SelectedItem;
            var name = ProfileNameText.Text;

            Profile? profile;
            if ((profile = Global.Settings.Profiles.SingleOrDefault(p => p.Index == index)) != null)
                Global.Settings.Profiles.Remove(profile);

            profile = new Profile(server, mode, name, index);
            Global.Settings.Profiles.Add(profile);
            return profile;
        }

        private async void ProfileButton_Click([NotNull] object? sender, EventArgs? e)
        {
            if (sender == null)
                throw new ArgumentNullException(nameof(sender));

            var profileButton = (Button)sender;
            var profile = (Profile?)profileButton.Tag;
            var index = ProfileTable.Controls.IndexOf(profileButton);

            switch (ModifierKeys)
            {
                case Keys.Control:
                    if (ServerComboBox.SelectedIndex == -1)
                    {
                        MessageBoxX.Show(i18N.Translate("Please select a server first"));
                        return;
                    }

                    if (ModeComboBox.SelectedIndex == -1)
                    {
                        MessageBoxX.Show(i18N.Translate("Please select a mode first"));
                        return;
                    }

                    if (ProfileNameText.Text == "")
                    {
                        MessageBoxX.Show(i18N.Translate("Please enter a profile name first"));
                        ProfileNameText.Focus();
                        return;
                    }

                    profileButton.Tag = profile = CreateProfileAtIndex(index);
                    profileButton.Text = profile.ProfileName;
                    ProfileNameText.Clear();
                    return;
                case Keys.Shift:
                    if (profile == null)
                        return;

                    Global.Settings.Profiles.Remove(profile);
                    profileButton.Tag = null;
                    profileButton.Text = i18N.Translate("None");
                    return;
            }

            if (profile == null)
            {
                MessageBoxX.Show(i18N.Translate("No saved profile here. Save a profile first by Ctrl+Click on the button"));
                return;
            }

            try
            {
                ActiveProfile(profile);
            }
            catch (Exception exception)
            {
                MessageBoxX.Show(exception.Message, LogLevel.ERROR);
                return;
            }

            // start the profile
            ControlButton_Click(null, null);
            if (State == State.Stopping || State == State.Stopped)
            {
                while (State != State.Stopped)
                    await Task.Delay(250);

                ControlButton_Click(null, null);
            }
        }

        #endregion

        #region State

        private State _state = State.Waiting;

        /// <summary>
        ///     当前状态
        /// </summary>
        public State State
        {
            get => _state;
            private set
            {
                void StartDisableItems(bool enabled)
                {
                    ServerComboBox.Enabled = ModeComboBox.Enabled = EditModePictureBox.Enabled =
                        EditServerPictureBox.Enabled = DeleteModePictureBox.Enabled = DeleteServerPictureBox.Enabled = enabled;

                    // 启动需要禁用的控件
                    ServerToolStripMenuItem.Enabled = ModeToolStripMenuItem.Enabled =
                        UninstallServiceToolStripMenuItem.Enabled = enabled;
                }

                _state = value;

                DelayTestHelper.Enabled = IsWaiting(_state);

                StatusText();
                switch (value)
                {
                    case State.Waiting:
                        ControlButton.Enabled = true;
                        ControlButton.Text = i18N.Translate("Start");

                        break;
                    case State.Starting:
                        ControlButton.Enabled = false;
                        ControlButton.Text = "...";

                        ProfileGroupBox.Enabled = false;
                        StartDisableItems(false);
                        break;
                    case State.Started:
                        ControlButton.Enabled = true;
                        ControlButton.Text = i18N.Translate("Stop");

                        ProfileGroupBox.Enabled = true;

                        break;
                    case State.Stopping:
                        ControlButton.Enabled = false;
                        ControlButton.Text = "...";

                        ProfileGroupBox.Enabled = false;
                        BandwidthState(false);
                        ConnectivityStatusVisible(false);
                        break;
                    case State.Stopped:
                        ControlButton.Enabled = true;
                        ControlButton.Text = i18N.Translate("Start");

                        LastUploadBandwidth = 0;
                        LastDownloadBandwidth = 0;
                        Bandwidth.Stop();

                        ProfileGroupBox.Enabled = true;
                        StartDisableItems(true);
                        break;
                }
            }
        }

        public async Task StopAsync()
        {
            if (IsWaiting())
                return;

            await StopCoreAsync();
        }

        private async Task StopCoreAsync()
        {
            State = State.Stopping;
            _startedPingCts?.Cancel();
            _discoveryNatCts?.Cancel();
            _httpConnectCts?.Cancel();
            await MainController.StopAsync();
            State = State.Stopped;
        }

        private bool IsWaiting() => IsWaiting(_state);

        private static bool IsWaiting(State state)
        {
            return state is State.Waiting or State.Stopped;
        }

        /// <summary>
        ///     更新状态栏文本
        /// </summary>
        /// <param name="text"></param>
        public void StatusText(string? text = null)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(StatusText), text);
                return;
            }

            text ??= i18N.Translate(StateExtension.GetStatusString(State));
            if (_state == State.Started)
                text += StatusPortInfoText.Value;

            StatusLabel.Text = i18N.Translate("Status", ": ") + text;
        }

        public void BandwidthState(bool state)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(BandwidthState), state);
                return;
            }

            if (IsWaiting())
                return;

            UsedBandwidthLabel.Visible /*= UploadSpeedLabel.Visible*/ = DownloadSpeedLabel.Visible = state;
        }

        private void UpdateNatTypeStatusLabelText(string? text, string? country = null)
        {
            if (!string.IsNullOrEmpty(text))
            {
                if (country == null)
                    NatTypeStatusLabel.Text = $"NAT{i18N.Translate(": ")}{text} ";
                else
                    NatTypeStatusLabel.Text = $"NAT{i18N.Translate(": ")}{text} [{country}]";

                UpdateNatTypeLight(int.TryParse(text, out var natType) ? natType : -1);
            }
            else
            {
                NatTypeStatusLabel.Text = $@"NAT{i18N.Translate(": ", "Test failed")}";
            }

            NatTypeStatusLabel.Visible = true;
        }

        private void ConnectivityStatusVisible(bool visible)
        {
            if (!visible)
                HttpStatusLabel.Text = NatTypeStatusLabel.Text = "";

            HttpStatusLabel.Visible = NatTypeStatusLabel.Visible = NatTypeStatusLightLabel.Visible = visible;
        }

        /// <summary>
        ///     更新 NAT指示灯颜色
        /// </summary>
        /// <param name="natType">NAT Type. keep default(-1) to Hide Light</param>
        private void UpdateNatTypeLight(int natType = -1)
        {
            if (natType > 0 && natType < 5)
            {
                NatTypeStatusLightLabel.Visible = Flags.IsWindows10Upper;
                var c = natType switch
                {
                    1 => Color.LimeGreen,
                    2 => Color.Yellow,
                    3 => Color.Red,
                    4 => Color.Black,
                    _ => throw new ArgumentOutOfRangeException(nameof(natType), natType, null)
                };

                NatTypeStatusLightLabel.ForeColor = c;
            }
            else
            {
                NatTypeStatusLightLabel.Visible = false;
            }
        }

        private async void TcpStatusLabel_Click(object sender, EventArgs e)
        {
            await HttpConnectAsync();
        }

        private async void NatTypeStatusLabel_Click(object sender, EventArgs e)
        {
            await DiscoveryNatTypeAsync();
        }

        private CancellationTokenSource? _discoveryNatCts;

        private async Task DiscoveryNatTypeAsync()
        {
            NatTypeStatusLabel.Enabled = false;
            UpdateNatTypeStatusLabelText("Testing NAT Type");

            _discoveryNatCts = new CancellationTokenSource();

            try
            {
                var res = await MainController.DiscoveryNatTypeAsync(_discoveryNatCts.Token);
                if (_discoveryNatCts.IsCancellationRequested)
                    return;

                if (!string.IsNullOrEmpty(res.PublicEnd))
                {
                    var country = await Utils.Utils.GetCityCodeAsync(res.PublicEnd);

                    UpdateNatTypeStatusLabelText(res.Result, country);
                    if (int.TryParse(res.Result, out var natType))
                        UpdateNatTypeLight(natType);
                    else
                        UpdateNatTypeLight();
                }
                else
                {
                    UpdateNatTypeStatusLabelText(res.Result ?? "Error");
                    NatTypeStatusLightLabel.Visible = false;
                }
            }
            finally
            {
                _discoveryNatCts.Dispose();
                _discoveryNatCts = null;
                NatTypeStatusLabel.Enabled = true;
            }
        }

        private CancellationTokenSource? _httpConnectCts;

        private async Task HttpConnectAsync()
        {
            HttpStatusLabel.Enabled = false;

            _httpConnectCts = new CancellationTokenSource();

            try
            {
                var res = await MainController.HttpConnectAsync(_httpConnectCts.Token);
                if (_httpConnectCts.IsCancellationRequested)
                    return;

                if (res != null)
                    HttpStatusLabel.Text = $"HTTP{i18N.Translate(": ")}{res}ms";
                else
                    HttpStatusLabel.Text = $"HTTP{i18N.Translate(": ", "Timeout")}";

                HttpStatusLabel.Visible = true;
            }
            finally
            {
                _httpConnectCts.Dispose();
                _httpConnectCts = null;
                HttpStatusLabel.Enabled = true;
            }
        }

        #endregion

        #endregion

        #region Misc

        #region PowerEvent

        private bool _resumeFlag;

        private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend: //操作系统即将挂起
                    if (!IsWaiting())
                    {
                        _resumeFlag = true;
                        Log.Information("操作系统即将挂起，自动停止");
                        ControlButton_Click(null, null);
                    }

                    break;
                case PowerModes.Resume: //操作系统即将从挂起状态继续
                    if (_resumeFlag)
                    {
                        _resumeFlag = false;
                        Log.Information("操作系统即将从挂起状态继续，自动重启");
                        ControlButton_Click(null, null);
                    }

                    break;
            }
        }

        #endregion

        private void Minimize()
        {
            // 使关闭时窗口向右下角缩小的效果
            WindowState = FormWindowState.Minimized;

            Hide();
        }

        public async void Exit(bool forceExit = false, bool saveConfiguration = true)
        {
            if (!IsWaiting() && !Global.Settings.StopWhenExited && !forceExit)
            {
                MessageBoxX.Show(i18N.Translate("Please press Stop button first"));

                NotifyIcon_MouseDoubleClick(null, null);
                return;
            }

            // State = State.Terminating;
            NotifyIcon.Visible = false;
            Hide();

            if (saveConfiguration)
                await Configuration.SaveAsync();

            await StopAsync();

            foreach (var file in new[] { Constants.TempConfig, Constants.TempRouteFile })
                if (File.Exists(file))
                    File.Delete(file);

            Dispose();
            Environment.Exit(Environment.ExitCode);
        }

        #region FormClosingButton

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && State != State.Terminating)
            {
                // 取消"关闭窗口"事件
                e.Cancel = true; // 取消关闭窗体 

                // 如果未勾选关闭窗口时退出，隐藏至右下角托盘图标
                if (!Global.Settings.ExitWhenClosed)
                    Minimize();
                // 如果勾选了关闭时退出，自动点击退出按钮
                else
                    Exit();
            }
        }

        #endregion

        #region NetTraffic

        /// <summary>
        ///     上一次下载的流量
        /// </summary>
        public ulong LastDownloadBandwidth;

        /// <summary>
        ///     上一次上传的流量
        /// </summary>
        public ulong LastUploadBandwidth;

        public void OnBandwidthUpdated(ulong download)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<ulong>(OnBandwidthUpdated), download);
                return;
            }

            try
            {
                UsedBandwidthLabel.Text = $"{i18N.Translate("Used", ": ")}{Bandwidth.Compute(download)}";
                //UploadSpeedLabel.Text = $"↑: {Utils.Bandwidth.Compute(upload - LastUploadBandwidth)}/s";
                DownloadSpeedLabel.Text = $"↑↓: {Bandwidth.Compute(download - LastDownloadBandwidth)}/s";

                //LastUploadBandwidth = upload;
                LastDownloadBandwidth = download;
                Refresh();
            }
            catch
            {
                // ignored
            }
        }

        #endregion

        #region NotifyIcon

        public void ShowMainFormToolStripButton_Click(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ShowMainFormToolStripButton_Click(sender, e)));
                return;
            }

            var forms = Application.OpenForms.Cast<Form>().ToList();
            var anyWindowOpened = forms.Any(f => f is not (MainForm or LogForm));

            forms.ForEach(f =>
            {
                if (anyWindowOpened && f is MainForm or LogForm)
                    return;

                f.Show();
                f.WindowState = FormWindowState.Normal;
                f.Activate();
            });
        }

        /// <summary>
        ///     通知图标右键菜单退出
        /// </summary>
        private void ExitToolStripButton_Click(object sender, EventArgs e)
        {
            Exit();
        }

        private void NotifyIcon_MouseDoubleClick(object? sender, MouseEventArgs? e)
        {
            ShowMainFormToolStripButton.PerformClick();
        }

        #endregion

        #region ComboBox_DrawItem

        private readonly SolidBrush _greenBrush = new(Color.FromArgb(50, 255, 56));
        private int _numberBoxWidth;
        private int _numberBoxX;
        private int _numberBoxWrap;

        private void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox cbx)
                return;

            // 绘制背景颜色
            e.Graphics.FillRectangle(Brushes.White, e.Bounds);

            if (e.Index < 0)
                return;

            // 绘制 备注/名称 字符串
            TextRenderer.DrawText(e.Graphics, cbx.Items[e.Index].ToString(), cbx.Font, e.Bounds, Color.Black, TextFormatFlags.Left);

            if (cbx.Items[e.Index] is not Server item)
                return;

            var brush = item.Delay switch { > 200 => Brushes.Red, > 80 => Brushes.Yellow, >= 0 => _greenBrush, _ => Brushes.Gray };
            e.Graphics.FillRectangle(brush, _numberBoxX, e.Bounds.Y, _numberBoxWidth, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics,
                item.Delay >= 0 ? item.Delay.ToString() : "--",
                cbx.Font,
                new Point(_numberBoxX + _numberBoxWrap, e.Bounds.Y),
                Color.Black,
                TextFormatFlags.Left);
        }

        #endregion

        #endregion
    }
}