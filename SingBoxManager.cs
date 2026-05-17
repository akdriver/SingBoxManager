using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private const string DashboardUrl = "http://127.0.0.1:9095/ui/#/proxies";
    private readonly string baseDir = AppDomain.CurrentDomain.BaseDirectory;
    private readonly string singBoxExe;
    private readonly string configFile;
    private readonly string profilesDir;
    private readonly string subscriptionUrlFile;
    private readonly string subscriptionLastUpdatedFile;

    private Panel sidebar;
    private Panel contentPanel;
    private Panel topBar;
    private Panel navPanel;
    private Label statusLabel;
    private Label titleLabel;
    private Label subtitleLabel;
    private Label sidebarBrandLabel;
    private PictureBox sidebarIconBox;
    private Label sidebarFooterLabel;
    private Button importButton;
    private Button startButton;
    private Button panelButton;
    private Button themeButton;
    private Button refreshButton;
    private Button logButton;
    private Button configSelectorButton;
    private TextBox subscriptionUrlTextBox;
    private Label subscriptionLastUpdatedLabel;
    private ConfigPopupForm configPopup;
    private ConfigItem selectedConfigItem;
    private Label coreStateValueLabel;
    private Label currentConfigValueLabel;
    private Label externalControllerValueLabel;
    private Label externalPortValueLabel;
    private Label secretValueLabel;
    private RoundedPanel commandBar;
    private Button commandBackButton;
    private Button commandRefreshButton;
    private Button commandStopButton;
    private Timer commandTimer;
    private WebView2 webView;
    private Process managedProcess;
    private bool closing;
    private PageKind currentPage = PageKind.Config;

    public MainForm()
    {
        singBoxExe = Path.Combine(baseDir, "sing-box.exe");
        configFile = Path.Combine(baseDir, "config.json");
        profilesDir = Path.Combine(baseDir, "profiles");
        subscriptionUrlFile = Path.Combine(profilesDir, "config-url.txt");
        subscriptionLastUpdatedFile = Path.Combine(profilesDir, "config-url-updated.txt");
        Theme.SetDark(IsSystemDarkMode());
        BuildWindow();
        Shown += delegate { Initialize(); };
        FormClosing += MainForm_FormClosing;
        KeyDown += MainForm_KeyDown;
    }

    private void BuildWindow()
    {
        Text = "Sing Box Manager";
        MinimumSize = new Size(1040, 700);
        Size = new Size(1180, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Window;
        Font = new Font("Microsoft YaHei UI", 9F);
        KeyPreview = true;

        string iconPath = Path.Combine(baseDir, "sing-box.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 224,
            BackColor = Theme.Sidebar
        };
        Controls.Add(sidebar);

        BuildSidebar();

        topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(24, 12, 24, 10),
            BackColor = Theme.Window
        };
        Controls.Add(topBar);
        BuildTopBar();

        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            BackColor = Theme.Window
        };
        Controls.Add(contentPanel);

        BuildCommandBar();
    }

    private void BuildCommandBar()
    {
        commandBar = new RoundedPanel
        {
            Radius = 22,
            Width = 356,
            Height = 46,
            Top = -56,
            BackColor = Theme.CommandBar,
            Visible = false
        };
        Controls.Add(commandBar);
        commandBar.BringToFront();

        commandBackButton = CreateCommandButton("←  返回配置");
        commandBackButton.Width = 116;
        commandBackButton.Left = 8;
        commandBackButton.Top = 7;
        commandBackButton.Click += delegate
        {
            ShowConfigPage("配置文件", "选择已导入配置，或导入新的 sing-box JSON 配置。");
        };
        commandBar.Controls.Add(commandBackButton);

        commandRefreshButton = CreateCommandButton("⟳  刷新");
        commandRefreshButton.Width = 90;
        commandRefreshButton.Left = 132;
        commandRefreshButton.Top = 7;
        commandRefreshButton.Click += delegate
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                webView.Reload();
            }
        };
        commandBar.Controls.Add(commandRefreshButton);

        commandStopButton = CreateCommandButton("⏻  停止");
        commandStopButton.Width = 90;
        commandStopButton.Left = 230;
        commandStopButton.Top = 7;
        commandStopButton.Click += delegate
        {
            StopSingBoxProcesses(true);
            ShowCorePage("核心已停止", "可以导入新的配置，或继续使用当前 config.json。");
        };
        commandBar.Controls.Add(commandStopButton);

        commandTimer = new Timer { Interval = 15 };
        commandTimer.Tick += delegate { AnimateCommandBar(); };
        commandTimer.Start();

        Resize += delegate { PositionCommandBar(); };
        PositionCommandBar();
    }

    private void BuildSidebar()
    {
        sidebarIconBox = new PictureBox
        {
            Left = 24,
            Top = 30,
            Width = 30,
            Height = 30,
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Theme.Sidebar
        };
        string iconPath = Path.Combine(baseDir, "sing-box.ico");
        if (File.Exists(iconPath))
        {
            sidebarIconBox.Image = RenderSidebarIcon(iconPath, 30);
        }
        sidebar.Controls.Add(sidebarIconBox);

        sidebarBrandLabel = new Label
        {
            Text = "Sing Box Manager",
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 11.5F),
            AutoSize = false,
            Left = 60,
            Top = 28,
            Width = 156,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0)
        };
        sidebar.Controls.Add(sidebarBrandLabel);

        navPanel = new Panel
        {
            Left = 14,
            Top = 94,
            Width = 196,
            Height = 190,
            BackColor = Theme.Sidebar
        };
        sidebar.Controls.Add(navPanel);

        importButton = CreateSidebarButton("配置文件");
        importButton.Top = 0;
        importButton.Click += delegate { ShowConfigPage("配置文件", "选择已导入配置，或导入新的 sing-box JSON 配置。"); };
        navPanel.Controls.Add(importButton);

        startButton = CreateSidebarButton("内核状态");
        startButton.Top = 50;
        startButton.Click += delegate { ShowCorePage("内核状态", "查看 sing-box 运行状态，并控制内核与面板。"); };
        navPanel.Controls.Add(startButton);

        panelButton = CreateSidebarButton("进入面板");
        panelButton.Top = 100;
        panelButton.Click += async delegate { await StartSingBoxAndOpenAsync(); };
        navPanel.Controls.Add(panelButton);

        themeButton = CreateSidebarButton(Theme.IsDark ? "☀  浅色模式" : "☾  深色模式");
        themeButton.Left = 14;
        themeButton.Top = sidebar.Height - 150;
        themeButton.Width = 196;
        themeButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        themeButton.Click += delegate { ToggleTheme(); };
        sidebar.Controls.Add(themeButton);

        sidebarFooterLabel = new Label
        {
            Left = 24,
            Top = sidebar.Height - 96,
            Width = 176,
            Height = 80,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            Padding = new Padding(0, 0, 0, 16),
            ForeColor = Theme.Muted,
            Text = "配置: config.json\r\n面板: 127.0.0.1:9095",
            TextAlign = ContentAlignment.BottomLeft
        };
        sidebar.Controls.Add(sidebarFooterLabel);
    }

    private void BuildTopBar()
    {
        titleLabel = new Label
        {
            Text = "控制台",
            ForeColor = Theme.Text,
            Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
            AutoSize = false,
            Height = 26,
            Left = 24,
            Top = 13,
            Width = 400
        };
        topBar.Controls.Add(titleLabel);

        subtitleLabel = new Label
        {
            Text = "导入配置后启动 sing-box，并在应用内查看本地面板",
            ForeColor = Theme.Subtle,
            AutoSize = false,
            Height = 22,
            Left = 24,
            Top = 40,
            Width = 560
        };
        topBar.Controls.Add(subtitleLabel);

        statusLabel = new Label
        {
            Text = "未启动",
            ForeColor = Theme.Muted,
            AutoSize = false,
            Width = 110,
            Height = 32,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Left = Width - 390,
            Top = 22,
            TextAlign = ContentAlignment.MiddleCenter
        };
        topBar.Controls.Add(statusLabel);

        refreshButton = CreateTopButton("刷新");
        refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        refreshButton.Left = Width - 272;
        refreshButton.Top = 21;
        refreshButton.Click += delegate
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                webView.Reload();
            }
        };
        topBar.Controls.Add(refreshButton);

        logButton = CreateTopButton("日志");
        logButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        logButton.Left = Width - 178;
        logButton.Top = 21;
        logButton.Click += delegate { OpenLog(); };
        topBar.Controls.Add(logButton);

        Resize += delegate
        {
            statusLabel.Left = ClientSize.Width - sidebar.Width - 380;
            refreshButton.Left = ClientSize.Width - sidebar.Width - 264;
            logButton.Left = ClientSize.Width - sidebar.Width - 170;
        };
    }

    private void Initialize()
    {
        Directory.CreateDirectory(profilesDir);
        EnsureProfileFromCurrentConfig();

        if (!File.Exists(singBoxExe))
        {
            ShowCorePage("找不到 sing-box.exe", "请把 sing-box.exe 放在当前应用目录后再启动。");
            return;
        }

        ShowConfigPage("配置文件", "选择已导入配置，或导入新的 sing-box JSON 配置。");
    }

    private void ShowImportScreen(string title, string body)
    {
        ShowConfigPage(title, body);
    }

    private void PreparePage(string pageTitle, string pageSubtitle)
    {
        commandBar.Visible = false;
        sidebar.Visible = true;
        sidebar.Width = 224;
        topBar.Visible = true;
        ApplyShellTheme();
        contentPanel.BringToFront();
        SetStatus(IsSingBoxAlreadyRunning() ? "运行中" : "未启动", IsSingBoxAlreadyRunning() ? Theme.Success : Theme.Muted);
        titleLabel.Text = pageTitle;
        subtitleLabel.Text = pageSubtitle;
        contentPanel.Controls.Clear();
    }

    private Panel CreatePageCanvas()
    {
        var canvas = new Panel
        {
            Width = 620,
            Height = 660,
            Anchor = AnchorStyles.None,
            BackColor = Theme.Window
        };
        canvas.Left = (contentPanel.ClientSize.Width - canvas.Width) / 2;
        canvas.Top = Math.Max(30, (contentPanel.ClientSize.Height - canvas.Height) / 2);
        contentPanel.Controls.Add(canvas);
        contentPanel.Resize += delegate
        {
            canvas.Left = (contentPanel.ClientSize.Width - canvas.Width) / 2;
            canvas.Top = Math.Max(30, (contentPanel.ClientSize.Height - canvas.Height) / 2);
        };
        return canvas;
    }

    private void AddPageHeader(Panel canvas, string title, string body)
    {

        var h = new Label
        {
            Text = title,
            ForeColor = Theme.Text,
            Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
            AutoSize = false,
            Left = 0,
            Top = 0,
            Width = 760,
            Height = 48
        };
        canvas.Controls.Add(h);

        var p = new Label
        {
            Text = body,
            ForeColor = Theme.Subtle,
            Font = new Font("Microsoft YaHei UI", 10F),
            AutoSize = false,
            Left = 2,
            Top = 50,
            Width = 740,
            Height = 28
        };
        canvas.Controls.Add(p);
    }

    private void ShowConfigPage(string title, string body)
    {
        currentPage = PageKind.Config;
        PreparePage("配置文件", "选择或导入配置文件");
        Panel canvas = CreatePageCanvas();
        AddPageHeader(canvas, title, body);

        var configCard = new RoundedPanel
        {
            Radius = 18,
            BackColor = Theme.Card,
            Left = 0,
            Top = 108,
            Width = 620,
            Height = 360
        };
        canvas.Controls.Add(configCard);

        var configTitle = CreateCardTitle("配置文件");
        configTitle.Left = 26;
        configTitle.Top = 24;
        configCard.Controls.Add(configTitle);

        var configDesc = CreateMutedLabel("选择已导入配置，或导入新的 JSON 配置。");
        configDesc.Left = 26;
        configDesc.Top = 58;
        configDesc.Width = 540;
        configCard.Controls.Add(configDesc);

        configSelectorButton = CreateConfigSelectorButton("选择配置文件");
        configSelectorButton.Left = 26;
        configSelectorButton.Top = 100;
        configSelectorButton.Width = 568;
        configSelectorButton.Height = 42;
        configSelectorButton.Click += delegate { ShowConfigMenu(); };
        configCard.Controls.Add(configSelectorButton);
        RefreshConfigSelector();

        var selectButton = CreateFlatButton("应用所选配置");
        selectButton.Left = 26;
        selectButton.Top = 154;
        selectButton.Width = 176;
        selectButton.Click += delegate { SelectImportedConfig(); };
        configCard.Controls.Add(selectButton);

        var importNewButton = CreatePrimaryButton("导入新配置");
        importNewButton.Left = 218;
        importNewButton.Top = 154;
        importNewButton.Width = 176;
        importNewButton.Click += delegate { ImportConfig(); };
        configCard.Controls.Add(importNewButton);

        var openCurrentButton = CreateFlatButton("打开当前配置");
        openCurrentButton.Left = 410;
        openCurrentButton.Top = 154;
        openCurrentButton.Width = 184;
        openCurrentButton.Click += delegate { OpenCurrentConfig(); };
        configCard.Controls.Add(openCurrentButton);

        currentConfigValueLabel = CreateMutedLabel("当前配置: " + GetCurrentConfigName());
        currentConfigValueLabel.Left = 26;
        currentConfigValueLabel.Top = 222;
        currentConfigValueLabel.Width = 540;
        configCard.Controls.Add(currentConfigValueLabel);

        ConfigSummary summary = ReadConfigSummary(configFile);

        externalControllerValueLabel = CreateMutedLabel("控制端口: " + summary.Controller);
        externalControllerValueLabel.Left = 26;
        externalControllerValueLabel.Top = 252;
        externalControllerValueLabel.Width = 540;
        configCard.Controls.Add(externalControllerValueLabel);

        externalPortValueLabel = CreateMutedLabel("UI 端口: " + summary.Port);
        externalPortValueLabel.Left = 26;
        externalPortValueLabel.Top = 280;
        externalPortValueLabel.Width = 180;
        configCard.Controls.Add(externalPortValueLabel);

        secretValueLabel = CreateMutedLabel("密码: " + summary.SecretDisplay);
        secretValueLabel.Left = 26;
        secretValueLabel.Top = 308;
        secretValueLabel.Width = 420;
        configCard.Controls.Add(secretValueLabel);

        var copySecretButton = CreateFlatButton("复制密码");
        copySecretButton.Left = 462;
        copySecretButton.Top = 302;
        copySecretButton.Width = 132;
        copySecretButton.Enabled = summary.HasSecret;
        copySecretButton.Click += delegate
        {
            if (summary.HasSecret)
            {
                Clipboard.SetText(summary.Secret);
                ShowConfigPage("已复制密码", "secret 已复制到剪切板。");
            }
        };
        configCard.Controls.Add(copySecretButton);

        var urlCard = new RoundedPanel
        {
            Radius = 18,
            BackColor = Theme.Card,
            Left = 0,
            Top = 486,
            Width = 620,
            Height = 158
        };
        canvas.Controls.Add(urlCard);

        var urlTitle = CreateCardTitle("URL 配置更新");
        urlTitle.Left = 26;
        urlTitle.Top = 22;
        urlCard.Controls.Add(urlTitle);

        var urlDesc = CreateMutedLabel("从 URL 下载配置并导入，之后可一键更新。");
        urlDesc.Left = 26;
        urlDesc.Top = 54;
        urlDesc.Width = 540;
        urlCard.Controls.Add(urlDesc);

        subscriptionUrlTextBox = CreateInputBox(GetSubscriptionUrl());
        subscriptionUrlTextBox.Left = 26;
        subscriptionUrlTextBox.Top = 84;
        subscriptionUrlTextBox.Width = 360;
        subscriptionUrlTextBox.Height = 34;
        urlCard.Controls.Add(subscriptionUrlTextBox);

        var importUrlButton = CreatePrimaryButton("下载导入");
        importUrlButton.Left = 398;
        importUrlButton.Top = 80;
        importUrlButton.Width = 96;
        importUrlButton.Height = 38;
        importUrlButton.Click += async delegate { await ImportConfigFromUrlAsync(false); };
        urlCard.Controls.Add(importUrlButton);

        var updateUrlButton = CreateFlatButton("更新");
        updateUrlButton.Left = 506;
        updateUrlButton.Top = 80;
        updateUrlButton.Width = 88;
        updateUrlButton.Height = 38;
        updateUrlButton.Click += async delegate { await ImportConfigFromUrlAsync(true); };
        urlCard.Controls.Add(updateUrlButton);

        subscriptionLastUpdatedLabel = CreateMutedLabel("上次更新: " + GetSubscriptionLastUpdatedDisplay());
        subscriptionLastUpdatedLabel.Left = 26;
        subscriptionLastUpdatedLabel.Top = 126;
        subscriptionLastUpdatedLabel.Width = 540;
        urlCard.Controls.Add(subscriptionLastUpdatedLabel);
    }

    private void ShowCorePage(string title, string body)
    {
        currentPage = PageKind.Core;
        PreparePage("内核状态", "启动、关闭、重启 sing-box 核心。");
        Panel canvas = CreatePageCanvas();
        AddPageHeader(canvas, title, body);

        var coreCard = new RoundedPanel
        {
            Radius = 18,
            BackColor = Theme.Card,
            Left = 0,
            Top = 108,
            Width = 620,
            Height = 270
        };
        canvas.Controls.Add(coreCard);

        var coreTitle = CreateCardTitle("内核状态");
        coreTitle.Left = 26;
        coreTitle.Top = 24;
        coreCard.Controls.Add(coreTitle);

        bool running = IsSingBoxAlreadyRunning();

        coreStateValueLabel = new Label
        {
            Text = running ? "运行中" : "未启动",
            ForeColor = running ? Theme.Success : Theme.Muted,
            Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
            AutoSize = false,
            Left = 26,
            Top = 66,
            Width = 540,
            Height = 48
        };
        coreCard.Controls.Add(coreStateValueLabel);

        var coreDesc = CreateMutedLabel("启动成功后会打开内置 9095 面板。");
        coreDesc.Left = 28;
        coreDesc.Top = 118;
        coreDesc.Width = 540;
        coreCard.Controls.Add(coreDesc);

        var startCoreButton = CreatePrimaryButton("启动内核");
        startCoreButton.Left = 26;
        startCoreButton.Top = 154;
        startCoreButton.Width = 132;
        startCoreButton.Enabled = !running;
        startCoreButton.Click += async delegate { await StartCoreOnlyAsync(); };
        coreCard.Controls.Add(startCoreButton);

        var stopCoreButton = CreateFlatButton("关闭内核");
        stopCoreButton.Left = 172;
        stopCoreButton.Top = 154;
        stopCoreButton.Width = 132;
        stopCoreButton.Enabled = running;
        stopCoreButton.Click += delegate
        {
            StopSingBoxProcesses(true);
            ShowCorePage("核心已关闭", "可以重新选择配置，或再次启动内核。");
        };
        coreCard.Controls.Add(stopCoreButton);

        var restartCoreButton = CreateFlatButton("重启内核");
        restartCoreButton.Left = 318;
        restartCoreButton.Top = 154;
        restartCoreButton.Width = 132;
        restartCoreButton.Enabled = running;
        restartCoreButton.Click += async delegate { await RestartSingBoxAsync(); };
        coreCard.Controls.Add(restartCoreButton);
    }

    private void ShowLoadingScreen(string message)
    {
        commandBar.Visible = false;
        sidebar.Visible = true;
        sidebar.Width = 224;
        topBar.Visible = true;
        contentPanel.BringToFront();
        SetStatus("启动中", Theme.Warning);
        titleLabel.Text = "控制台";
        subtitleLabel.Text = message;
        contentPanel.Controls.Clear();

        var label = new Label
        {
            Text = message,
            ForeColor = Theme.Subtle,
            Font = new Font("Microsoft YaHei UI", 13F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        contentPanel.Controls.Add(label);
    }

    private void ImportConfig()
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Title = "选择 sing-box 配置文件";
            dialog.Filter = "JSON 配置 (*.json)|*.json|所有文件 (*.*)|*.*";
            dialog.Multiselect = false;

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            ShowLoadingScreen("正在导入并校验配置...");
            string backup = null;
            try
            {
                backup = BackupConfig();
                File.Copy(dialog.FileName, configFile, true);

                string checkOutput;
                if (!RunSingBoxCheck(out checkOutput))
                {
                    RestoreBackup(backup);
                    ShowImportScreen("配置校验失败", TrimOutput(checkOutput));
                    return;
                }

                if (!HasControllerPort(configFile))
                {
                    RestoreBackup(backup);
                    ShowImportScreen("配置缺少面板端口", "请导入包含 external_controller 9095 的配置文件。");
                    return;
                }

                SaveProfileCopy(dialog.FileName);
                ShowConfigPage("配置已导入", "配置校验通过。可以启动内核，或继续选择其他已导入配置。");
            }
            catch (Exception ex)
            {
                RestoreBackup(backup);
                ShowConfigPage("导入失败", ex.Message);
            }
        }
    }

    private async Task ImportConfigFromUrlAsync(bool useSavedUrl)
    {
        string url = useSavedUrl ? GetSubscriptionUrl() : (subscriptionUrlTextBox != null ? subscriptionUrlTextBox.Text.Trim() : "");
        Uri uri;
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ShowConfigPage("URL 无效", "请输入 http 或 https 开头的配置文件 URL。");
            return;
        }

        ShowLoadingScreen("正在从 URL 下载并校验配置...");
        string backup = null;
        string downloadedConfig = Path.Combine(baseDir, "config.download." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");

        try
        {
            backup = BackupConfig();

            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "SingBoxManager/1.0";
                await client.DownloadFileTaskAsync(uri, downloadedConfig);
            }

            File.Copy(downloadedConfig, configFile, true);

            string checkOutput;
            if (!RunSingBoxCheck(out checkOutput))
            {
                RestoreBackup(backup);
                ShowConfigPage("配置校验失败", TrimOutput(checkOutput));
                return;
            }

            if (!HasControllerPort(configFile))
            {
                RestoreBackup(backup);
                ShowConfigPage("配置缺少面板端口", "请导入包含 external_controller 9095 的配置文件。");
                return;
            }

            SaveSubscriptionInfo(url);
            SaveUrlProfileCopy();
            ShowConfigPage("URL 配置已更新", "配置已下载并通过校验。上次更新时间已保存。");
        }
        catch (Exception ex)
        {
            RestoreBackup(backup);
            ShowConfigPage("URL 更新失败", ex.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(downloadedConfig))
                {
                    File.Delete(downloadedConfig);
                }
            }
            catch
            {
            }
        }
    }

    private void SelectImportedConfig()
    {
        ConfigItem item = selectedConfigItem;
        if (item == null || !File.Exists(item.Path))
        {
            ShowConfigPage("未选择配置", "请先选择一个已导入的配置文件。");
            return;
        }

        string backup = null;
        try
        {
            backup = BackupConfig();
            if (!string.Equals(Path.GetFullPath(item.Path), Path.GetFullPath(configFile), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(item.Path, configFile, true);
            }

            string checkOutput;
            if (!RunSingBoxCheck(out checkOutput))
            {
                RestoreBackup(backup);
                ShowConfigPage("配置校验失败", TrimOutput(checkOutput));
                return;
            }

            if (!HasControllerPort(configFile))
            {
                RestoreBackup(backup);
                ShowConfigPage("配置缺少面板端口", "请选择包含 external_controller 9095 的配置文件。");
                return;
            }

            ShowConfigPage("配置已选择", "当前配置已切换为 " + item.Name + "。");
        }
        catch (Exception ex)
        {
            RestoreBackup(backup);
            ShowConfigPage("选择失败", ex.Message);
        }
    }

    private async Task RestartSingBoxAsync()
    {
        StopSingBoxProcesses(false);
        await StartCoreOnlyAsync();
    }

    private async Task StartCoreOnlyAsync()
    {
        if (await StartCoreAsync())
        {
            ShowCorePage("内核已启动", "sing-box 正在运行。可以从左侧进入面板。");
        }
    }

    private async Task StartSingBoxAndOpenAsync()
    {
        if (!IsSingBoxAlreadyRunning() && !await StartCoreAsync())
        {
            return;
        }

        ShowLoadingScreen("正在连接本地面板...");
        if (await WaitForDashboardAsync())
        {
            await ShowDashboardAsync();
            return;
        }

        ShowCorePage("面板连接失败", "sing-box 已尝试启动，但 127.0.0.1:9095 暂时不可访问。请检查配置或日志。");
    }

    private async Task<bool> StartCoreAsync()
    {
        if (!File.Exists(configFile))
        {
            ShowConfigPage("导入配置文件", "当前目录没有 config.json。请先导入或选择配置。");
            return false;
        }

        if (!HasControllerPort(configFile))
        {
            ShowConfigPage("配置缺少面板端口", "当前 config.json 没有检测到 9095 面板端口。");
            return false;
        }

        string checkOutput;
        if (!RunSingBoxCheck(out checkOutput))
        {
            ShowConfigPage("配置校验失败", TrimOutput(checkOutput));
            return false;
        }

        ShowLoadingScreen("正在启动 sing-box 内核...");
        try
        {
            if (!IsSingBoxAlreadyRunning())
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = singBoxExe,
                    Arguments = "run -c \"" + configFile + "\"",
                    WorkingDirectory = baseDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                managedProcess = Process.Start(startInfo);
            }

            await Task.Delay(500);
            return true;
        }
        catch (Exception ex)
        {
            ShowCorePage("启动失败", ex.Message);
            return false;
        }
    }

    private async Task ShowDashboardAsync()
    {
        SetStatus("运行中", Theme.Success);
        titleLabel.Text = "代理面板";
        subtitleLabel.Text = DashboardUrl;
        sidebar.Visible = false;
        topBar.Visible = false;
        commandBar.Visible = false;
        commandBar.Top = -56;
        PositionCommandBar();
        contentPanel.BringToFront();
        contentPanel.Controls.Clear();

        webView = new WebView2
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window
        };
        contentPanel.Controls.Add(webView);
        commandBar.BringToFront();

        try
        {
            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
            webView.CoreWebView2.Navigate(DashboardUrl);
        }
        catch (Exception ex)
        {
            contentPanel.Controls.Clear();
            ShowCorePage("WebView2 初始化失败", ex.Message);
        }
    }

    private bool RunSingBoxCheck(out string output)
    {
        return RunSingBoxCheck(configFile, out output);
    }

    private bool RunSingBoxCheck(string path, out string output)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = singBoxExe,
            Arguments = "check -c \"" + path + "\"",
            WorkingDirectory = baseDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using (Process process = Process.Start(startInfo))
        {
            output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(10000);
            return process.HasExited && process.ExitCode == 0;
        }
    }

    private async Task<bool> WaitForDashboardAsync()
    {
        for (int i = 0; i < 24; i++)
        {
            if (await IsDashboardReadyAsync())
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private static Task<bool> IsDashboardReadyAsync()
    {
        return Task.Run(delegate
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:9095/");
                request.Method = "GET";
                request.Timeout = 1000;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return (int)response.StatusCode < 500;
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (response)
                    {
                        return (int)response.StatusCode < 500;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        });
    }

    private bool HasControllerPort(string path)
    {
        string json = File.ReadAllText(path);
        return Regex.IsMatch(json, "\"external_controller\"\\s*:\\s*\"(?:127\\.0\\.0\\.1|0\\.0\\.0\\.0|localhost)?\\s*:?9095\"", RegexOptions.IgnoreCase)
            || Regex.IsMatch(json, "\"external_controller\"\\s*:\\s*\"[^\"]*:9095\"", RegexOptions.IgnoreCase);
    }

    private ConfigSummary ReadConfigSummary(string path)
    {
        if (!File.Exists(path))
        {
            return new ConfigSummary("未选择", "-", "");
        }

        string json = File.ReadAllText(path);
        string controller = MatchJsonString(json, "external_controller");
        string secret = MatchJsonString(json, "secret");
        string port = ExtractPort(controller);
        return new ConfigSummary(controller, port, secret);
    }

    private static string MatchJsonString(string json, string propertyName)
    {
        Match match = Regex.Match(
            json,
            "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return "";
        }

        return Regex.Unescape(match.Groups["value"].Value);
    }

    private static string ExtractPort(string controller)
    {
        if (string.IsNullOrEmpty(controller))
        {
            return "-";
        }

        Match match = Regex.Match(controller, ":(?<port>\\d+)\\s*$");
        return match.Success ? match.Groups["port"].Value : "-";
    }

    private string BackupConfig()
    {
        if (!File.Exists(configFile))
        {
            return null;
        }

        string backup = Path.Combine(baseDir, "config.backup." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
        File.Copy(configFile, backup, true);
        return backup;
    }

    private void EnsureProfileFromCurrentConfig()
    {
        if (!File.Exists(configFile))
        {
            return;
        }

        Directory.CreateDirectory(profilesDir);
        string profile = Path.Combine(profilesDir, "config.json");
        if (!File.Exists(profile))
        {
            File.Copy(configFile, profile, true);
        }
    }

    private void SaveProfileCopy(string sourcePath)
    {
        Directory.CreateDirectory(profilesDir);
        string name = Path.GetFileName(sourcePath);
        if (string.IsNullOrEmpty(name))
        {
            name = "config.json";
        }

        string target = Path.Combine(profilesDir, name);
        if (File.Exists(target))
        {
            string fileName = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name);
            target = Path.Combine(profilesDir, fileName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + extension);
        }

        File.Copy(configFile, target, true);
    }

    private void SaveUrlProfileCopy()
    {
        Directory.CreateDirectory(profilesDir);
        File.Copy(configFile, Path.Combine(profilesDir, "url-config.json"), true);
    }

    private string GetSubscriptionUrl()
    {
        if (!File.Exists(subscriptionUrlFile))
        {
            return "";
        }

        return File.ReadAllText(subscriptionUrlFile).Trim();
    }

    private void SaveSubscriptionInfo(string url)
    {
        Directory.CreateDirectory(profilesDir);
        File.WriteAllText(subscriptionUrlFile, url.Trim());
        File.WriteAllText(subscriptionLastUpdatedFile, DateTime.Now.ToString("o"));
    }

    private string GetSubscriptionLastUpdatedDisplay()
    {
        if (!File.Exists(subscriptionLastUpdatedFile))
        {
            return "从未更新";
        }

        DateTime updatedAt;
        string text = File.ReadAllText(subscriptionLastUpdatedFile).Trim();
        if (!DateTime.TryParse(text, out updatedAt))
        {
            return text;
        }

        return updatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void RefreshConfigSelector()
    {
        List<ConfigItem> items = GetConfigItems();
        if (items.Count == 0)
        {
            selectedConfigItem = null;
            if (configSelectorButton != null)
            {
                configSelectorButton.Text = "暂无已导入配置";
            }

            return;
        }

        selectedConfigItem = items[0];
        if (configSelectorButton != null)
        {
            configSelectorButton.Text = selectedConfigItem.Name;
        }
    }

    private List<ConfigItem> GetConfigItems()
    {
        var items = new List<ConfigItem>();

        if (File.Exists(configFile))
        {
            items.Add(new ConfigItem("当前配置 config.json", configFile));
        }

        if (Directory.Exists(profilesDir))
        {
            foreach (string file in Directory.GetFiles(profilesDir, "*.json"))
            {
                items.Add(new ConfigItem(Path.GetFileName(file), file));
            }
        }

        return items;
    }

    private void ShowConfigMenu()
    {
        if (configSelectorButton == null)
        {
            return;
        }

        if (configPopup != null && !configPopup.IsDisposed)
        {
            configPopup.Close();
            configPopup.Dispose();
        }

        List<ConfigItem> items = GetConfigItems();
        Point location = configSelectorButton.PointToScreen(new Point(0, configSelectorButton.Height + 8));
        configPopup = new ConfigPopupForm(items, configSelectorButton.Width);
        configPopup.ItemSelected += delegate(ConfigItem item)
        {
            selectedConfigItem = item;
            if (selectedConfigItem != null)
            {
                    configSelectorButton.Text = selectedConfigItem.Name;
            }
        };
        configPopup.StartPosition = FormStartPosition.Manual;
        configPopup.Location = location;
        configPopup.Show(this);
    }

    private void OpenCurrentConfig()
    {
        if (!File.Exists(configFile))
        {
            ShowConfigPage("找不到配置文件", "当前目录没有 config.json。");
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = configFile, UseShellExecute = true });
    }

    private string GetCurrentConfigName()
    {
        return File.Exists(configFile) ? "config.json" : "未选择";
    }

    private void RestoreBackup(string backup)
    {
        if (!string.IsNullOrEmpty(backup) && File.Exists(backup))
        {
            File.Copy(backup, configFile, true);
        }
    }

    private bool IsSingBoxAlreadyRunning()
    {
        return Process.GetProcessesByName("sing-box").Length > 0;
    }

    private void StopSingBoxProcesses(bool showMessage)
    {
        int foundCount = 0;
        int closedCount = 0;
        int failedCount = 0;
        string lastError = "";

        foreach (Process process in Process.GetProcessesByName("sing-box"))
        {
            foundCount++;
            try
            {
                using (process)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                        closedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                lastError = ex.Message;
                process.Dispose();
            }
        }

        managedProcess = null;
        SetStatus(IsSingBoxAlreadyRunning() ? "运行中" : "未启动", IsSingBoxAlreadyRunning() ? Theme.Success : Theme.Muted);
        if (showMessage)
        {
            if (closedCount > 0)
            {
                MessageBox.Show(this, "已停止 sing-box。", "Sing Box Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (foundCount > 0 && failedCount > 0)
            {
                MessageBox.Show(
                    this,
                    "检测到 sing-box 正在运行，但当前权限无法关闭它。\n\n请用管理员身份运行 Sing Box Manager 后再关闭核心。\n\n错误: " + lastError,
                    "Sing Box Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this, "sing-box 没有在运行。", "Sing Box Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    private void OpenLog()
    {
        string log = Path.Combine(baseDir, "sing-box.log");
        if (!File.Exists(log))
        {
            MessageBox.Show(this, "当前目录没有 sing-box.log。", "日志", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = log, UseShellExecute = true });
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (closing || !IsSingBoxAlreadyRunning())
        {
            return;
        }

        DialogResult result = MessageBox.Show(this, "关闭应用时是否同时停止 sing-box？", "退出", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        closing = true;
        if (result == DialogResult.Yes)
        {
            StopSingBoxProcesses(false);
        }
    }

    private void MainForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (HandleShortcut(e.KeyCode, e.Control))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        bool control = (keyData & Keys.Control) == Keys.Control;
        if (HandleShortcut(key, control))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool HandleShortcut(Keys key, bool control)
    {
        if (sidebar.Visible)
        {
            return false;
        }

        if (key == Keys.Escape)
        {
            ShowConfigPage("配置文件", "选择已导入配置，或导入新的 sing-box JSON 配置。");
            return true;
        }

        if (key == Keys.F5)
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                webView.Reload();
            }

            return true;
        }

        if (control && key == Keys.Q)
        {
            StopSingBoxProcesses(true);
            ShowCorePage("核心已停止", "可以导入新的配置，或继续使用当前 config.json。");
            return true;
        }

        return false;
    }

    private void SetStatus(string text, Color color)
    {
        if (statusLabel == null)
        {
            return;
        }

        statusLabel.Text = text;
        statusLabel.ForeColor = color;
    }

    private void ToggleTheme()
    {
        Theme.SetDark(!Theme.IsDark);
        ApplyShellTheme();
        sidebar.Controls.Clear();
        BuildSidebar();

        if (currentPage == PageKind.Core)
        {
            ShowCorePage("内核状态", "查看 sing-box 运行状态，并控制内核与面板。");
        }
        else
        {
            ShowConfigPage("配置文件", "选择已导入配置，或导入新的 sing-box JSON 配置。");
        }
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            object value = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);

            return value is int && (int)value == 0;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyShellTheme()
    {
        BackColor = Theme.Window;
        if (sidebar != null)
        {
            sidebar.BackColor = Theme.Sidebar;
        }

        if (topBar != null)
        {
            topBar.BackColor = Theme.Window;
        }

        if (contentPanel != null)
        {
            contentPanel.BackColor = Theme.Window;
        }

        if (navPanel != null)
        {
            navPanel.BackColor = Theme.Sidebar;
        }

        if (titleLabel != null)
        {
            titleLabel.ForeColor = Theme.Text;
        }

        if (subtitleLabel != null)
        {
            subtitleLabel.ForeColor = Theme.Subtle;
        }

        if (sidebarBrandLabel != null)
        {
            sidebarBrandLabel.ForeColor = Theme.Text;
        }

        if (sidebarIconBox != null)
        {
            sidebarIconBox.BackColor = Theme.Sidebar;
        }

        if (sidebarFooterLabel != null)
        {
            sidebarFooterLabel.ForeColor = Theme.Muted;
        }

        ApplyTopButtonTheme(refreshButton);
        ApplyTopButtonTheme(logButton);
        ApplyCommandBarTheme();
    }

    private void ApplyTopButtonTheme(Button button)
    {
        ModernButton modern = button as ModernButton;
        if (modern == null)
        {
            return;
        }

        modern.FillColor = Theme.Card;
        modern.HoverFillColor = Theme.CardAlt;
        modern.PressedFillColor = Theme.Line;
        modern.BorderColor = Theme.Line;
        modern.TextColor = Theme.Text;
        modern.Invalidate();
    }

    private void ApplyCommandBarTheme()
    {
        if (commandBar != null)
        {
            commandBar.BackColor = Theme.CommandBar;
        }

        ApplyCommandButtonTheme(commandBackButton);
        ApplyCommandButtonTheme(commandRefreshButton);
        ApplyCommandButtonTheme(commandStopButton);
    }

    private void ApplyCommandButtonTheme(Button button)
    {
        ModernButton modern = button as ModernButton;
        if (modern == null)
        {
            return;
        }

        modern.FillColor = Theme.CommandButton;
        modern.HoverFillColor = Theme.CommandButtonHover;
        modern.PressedFillColor = Theme.CommandButtonPressed;
        modern.BorderColor = Theme.CommandButtonBorder;
        modern.TextColor = Color.White;
        modern.Invalidate();
    }

    private static Bitmap RenderSidebarIcon(string iconPath, int size)
    {
        using (Icon icon = new Icon(iconPath, 256, 256))
        using (Bitmap source = icon.ToBitmap())
        {
            var target = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(target))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, size, size));
            }

            return target;
        }
    }

    private void PositionCommandBar()
    {
        if (commandBar == null)
        {
            return;
        }

        commandBar.Left = Math.Max(12, (ClientSize.Width - commandBar.Width) / 2);
    }

    private void AnimateCommandBar()
    {
        if (commandBar == null || sidebar == null || sidebar.Visible)
        {
            if (commandBar != null)
            {
                commandBar.Visible = false;
            }

            return;
        }

        Point point = PointToClient(Cursor.Position);
        bool shouldShow = point.Y <= 12 || commandBar.Bounds.Contains(point);
        int targetTop = shouldShow ? 12 : -56;

        if (commandBar.Top == targetTop)
        {
            commandBar.Visible = shouldShow;
            return;
        }

        commandBar.Visible = true;
        int delta = targetTop - commandBar.Top;
        commandBar.Top += Math.Sign(delta) * Math.Min(Math.Abs(delta), 8);
        commandBar.BringToFront();
    }

    private static string TrimOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "sing-box 没有返回详细错误。";
        }

        text = text.Trim();
        return text.Length > 180 ? text.Substring(0, 180) + "..." : text;
    }

    private static Label CreateCardTitle(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = Theme.Text,
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            AutoSize = false,
            Width = 300,
            Height = 32
        };
    }

    private static Label CreateMutedLabel(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = Theme.Subtle,
            Font = new Font("Microsoft YaHei UI", 9F),
            AutoSize = false,
            Height = 24
        };
    }

    private static Button CreateSidebarButton(string text)
    {
        return new ModernButton
        {
            Text = text,
            Width = 196,
            Height = 38,
            Left = 0,
            FillColor = Theme.SidebarButton,
            HoverFillColor = Theme.SidebarButtonHover,
            PressedFillColor = Theme.SidebarButtonPressed,
            BorderColor = Theme.Line,
            TextColor = Theme.Text,
            Radius = 10,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            Cursor = Cursors.Hand
        };
    }

    private static Button CreateTopButton(string text)
    {
        return new ModernButton
        {
            Text = text,
            Width = 82,
            Height = 32,
            FillColor = Theme.Card,
            HoverFillColor = Theme.CardAlt,
            PressedFillColor = Theme.Line,
            BorderColor = Theme.Line,
            TextColor = Theme.Text,
            Radius = 9,
            Cursor = Cursors.Hand
        };
    }

    private static Button CreateCommandButton(string text)
    {
        return new ModernButton
        {
            Text = text,
            Height = 32,
            FillColor = Theme.CommandButton,
            HoverFillColor = Theme.CommandButtonHover,
            PressedFillColor = Theme.CommandButtonPressed,
            BorderColor = Theme.CommandButtonBorder,
            TextColor = Color.White,
            Radius = 16,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }

    private static Button CreateConfigSelectorButton(string text)
    {
        return new ModernButton
        {
            Text = text,
            FillColor = Theme.CardAlt,
            HoverFillColor = Theme.SidebarButtonHover,
            PressedFillColor = Theme.SidebarButtonPressed,
            BorderColor = Theme.Line,
            TextColor = Theme.Text,
            Radius = 12,
            ShowsChevron = true,
            Font = new Font("Microsoft YaHei UI", 10F),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 48, 0)
        };
    }

    private static Button CreatePrimaryButton(string text)
    {
        return new ModernButton
        {
            Text = text,
            Height = 42,
            FillColor = Theme.Accent,
            HoverFillColor = Theme.AccentHover,
            PressedFillColor = Theme.AccentPressed,
            BorderColor = Theme.Accent,
            TextColor = Color.White,
            Radius = 12,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }

    private static Button CreateFlatButton(string text)
    {
        return new ModernButton
        {
            Text = text,
            Height = 42,
            FillColor = Theme.CardAlt,
            HoverFillColor = Theme.Line,
            PressedFillColor = Color.FromArgb(210, 219, 231),
            BorderColor = Theme.Line,
            TextColor = Theme.Text,
            Radius = 12,
            Font = new Font("Microsoft YaHei UI", 10F),
            Cursor = Cursors.Hand
        };
    }

    private static TextBox CreateInputBox(string text)
    {
        return new TextBox
        {
            Text = text,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.CardAlt,
            ForeColor = Theme.Text,
            Font = new Font("Microsoft YaHei UI", 10F),
            Multiline = false
        };
    }
}

internal sealed class ModernButton : Button
{
    private bool hovered;
    private bool pressed;

    public int Radius { get; set; }
    public Color FillColor { get; set; }
    public Color HoverFillColor { get; set; }
    public Color PressedFillColor { get; set; }
    public Color BorderColor { get; set; }
    public Color TextColor { get; set; }
    public bool ShowsChevron { get; set; }

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent != null ? Parent.BackColor : Color.Transparent);

        Color fill = !Enabled ? Theme.DisabledFill : pressed ? PressedFillColor : hovered ? HoverFillColor : FillColor;
        Color text = !Enabled ? Theme.DisabledText : TextColor;
        Color border = !Enabled ? Theme.Line : BorderColor;
        Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = RoundedRect(rect, Radius))
        using (SolidBrush brush = new SolidBrush(fill))
        using (Pen pen = new Pen(border))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

        if (ShowsChevron)
        {
            DrawChevron(e.Graphics, rect, text);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            Padding == Padding.Empty ? rect : new Rectangle(Padding.Left, 0, Width - Padding.Left - Padding.Right, Height),
            text,
            GetTextFlags());
    }

    private void DrawChevron(Graphics graphics, Rectangle rect, Color color)
    {
        Rectangle chip = new Rectangle(rect.Right - 35, rect.Top + 8, 24, rect.Height - 16);
        using (GraphicsPath chipPath = RoundedRect(chip, 10))
        using (SolidBrush chipBrush = new SolidBrush(Theme.IsDark ? Color.FromArgb(42, 50, 64) : Color.FromArgb(226, 234, 246)))
        {
            graphics.FillPath(chipBrush, chipPath);
        }

        int cx = chip.Left + chip.Width / 2;
        int cy = chip.Top + chip.Height / 2;
        Point[] points =
        {
            new Point(cx - 4, cy - 2),
            new Point(cx + 4, cy - 2),
            new Point(cx, cy + 3)
        };

        using (SolidBrush arrowBrush = new SolidBrush(color))
        {
            graphics.FillPolygon(arrowBrush, points);
        }
    }

    private TextFormatFlags GetTextFlags()
    {
        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        if (TextAlign == ContentAlignment.MiddleLeft)
        {
            return flags | TextFormatFlags.Left;
        }

        return flags | TextFormatFlags.HorizontalCenter;
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        int r = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, r, r, 180, 90);
        path.AddArc(rect.Right - r, rect.Top, r, r, 270, 90);
        path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - r, r, r, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ConfigItem
{
    public string Name { get; private set; }
    public string Path { get; private set; }

    public ConfigItem(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public override string ToString()
    {
        return Name;
    }
}

internal sealed class ConfigSummary
{
    public string Controller { get; private set; }
    public string Port { get; private set; }
    public string Secret { get; private set; }

    public bool HasSecret
    {
        get { return !string.IsNullOrEmpty(Secret); }
    }

    public string SecretDisplay
    {
        get
        {
            if (!HasSecret)
            {
                return "未设置";
            }

            if (Secret.Length <= 8)
            {
                return Secret;
            }

            return Secret.Substring(0, 4) + "..." + Secret.Substring(Secret.Length - 4);
        }
    }

    public ConfigSummary(string controller, string port, string secret)
    {
        Controller = string.IsNullOrEmpty(controller) ? "未设置" : controller;
        Port = string.IsNullOrEmpty(port) ? "-" : port;
        Secret = secret ?? "";
    }
}

internal sealed class ConfigPopupForm : Form
{
    public event Action<ConfigItem> ItemSelected;
    private readonly int popupWidth;

    public ConfigPopupForm(List<ConfigItem> items, int width)
    {
        popupWidth = width;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Theme.Card;
        Padding = new Padding(8);
        Width = popupWidth;
        Height = Math.Max(56, Math.Min(280, 16 + Math.Max(1, items.Count) * 42));
        BuildItems(items);
    }

    private void BuildItems(List<ConfigItem> items)
    {
        if (items.Count == 0)
        {
            var empty = new Label
            {
                Text = "暂无已导入配置",
                ForeColor = Theme.Muted,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                Left = 14,
                Top = 16,
                Width = Width - 28,
                Height = 24
            };
            Controls.Add(empty);
            return;
        }

        int top = 8;
        foreach (ConfigItem item in items)
        {
            var button = new ModernButton
            {
                Text = item.Name,
                Left = 8,
                Top = top,
                Width = Width - 16,
                Height = 34,
                FillColor = Theme.Card,
                HoverFillColor = Theme.SidebarButtonHover,
                PressedFillColor = Theme.SidebarButtonPressed,
                BorderColor = Theme.Card,
                TextColor = Theme.Text,
                Radius = 10,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 12, 0),
                Font = new Font("Microsoft YaHei UI", 9.5F),
                Cursor = Cursors.Hand,
                Tag = item
            };
            button.Click += delegate(object sender, EventArgs e)
            {
                ModernButton clicked = sender as ModernButton;
                ConfigItem selected = clicked != null ? clicked.Tag as ConfigItem : null;
                if (selected != null && ItemSelected != null)
                {
                    ItemSelected(selected);
                }

                Close();
            };
            Controls.Add(button);
            top += 42;
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        Close();
        base.OnDeactivate(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 14))
        using (SolidBrush brush = new SolidBrush(Theme.Card))
        using (Pen pen = new Pen(Theme.Line))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
            Region = new Region(path);
        }
        base.OnPaint(e);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        int r = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, r, r, 180, 90);
        path.AddArc(rect.Right - r, rect.Top, r, r, 270, 90);
        path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - r, r, r, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal enum PageKind
{
    Config,
    Core
}

internal static class ButtonExtensions
{
    public static Button WithFlatBorder(this Button button, Color color)
    {
        button.FlatAppearance.BorderColor = color;
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(button.BackColor);
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(button.BackColor);
        return button;
    }
}

internal sealed class RoundedPanel : Panel
{
    public int Radius { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (GraphicsPath path = new GraphicsPath())
        {
            int r = Radius;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            path.AddArc(rect.Left, rect.Top, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Top, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            Region = new Region(path);
        }
    }
}

internal static class Theme
{
    public static bool IsDark { get; private set; }
    public static Color Window;
    public static Color Sidebar;
    public static Color SidebarButton;
    public static Color SidebarButtonHover;
    public static Color SidebarButtonPressed;
    public static Color Card;
    public static Color CardAlt;
    public static Color Line;
    public static Color Text;
    public static Color Subtle;
    public static Color Muted;
    public static Color Accent;
    public static Color AccentHover;
    public static Color AccentPressed;
    public static Color Success;
    public static Color Warning;
    public static Color DisabledFill;
    public static Color DisabledText;
    public static Color CommandBar;
    public static Color CommandButton;
    public static Color CommandButtonHover;
    public static Color CommandButtonPressed;
    public static Color CommandButtonBorder;

    static Theme()
    {
        SetDark(false);
    }

    public static void SetDark(bool dark)
    {
        IsDark = dark;
        if (dark)
        {
            Window = Color.FromArgb(15, 18, 24);
            Sidebar = Color.FromArgb(18, 22, 29);
            SidebarButton = Color.FromArgb(25, 30, 39);
            SidebarButtonHover = Color.FromArgb(34, 41, 53);
            SidebarButtonPressed = Color.FromArgb(44, 53, 68);
            Card = Color.FromArgb(24, 29, 38);
            CardAlt = Color.FromArgb(31, 38, 49);
            Line = Color.FromArgb(49, 59, 74);
            Text = Color.FromArgb(235, 239, 245);
            Subtle = Color.FromArgb(166, 176, 190);
            Muted = Color.FromArgb(118, 130, 148);
            Accent = Color.FromArgb(80, 142, 255);
            AccentHover = Color.FromArgb(100, 156, 255);
            AccentPressed = Color.FromArgb(58, 118, 230);
            Success = Color.FromArgb(74, 222, 128);
            Warning = Color.FromArgb(251, 191, 36);
            DisabledFill = Color.FromArgb(29, 35, 44);
            DisabledText = Color.FromArgb(97, 108, 124);
            CommandBar = Color.FromArgb(21, 24, 31);
            CommandButton = Color.FromArgb(37, 43, 55);
            CommandButtonHover = Color.FromArgb(50, 59, 75);
            CommandButtonPressed = Color.FromArgb(63, 74, 94);
            CommandButtonBorder = Color.FromArgb(75, 86, 105);
        }
        else
        {
            Window = Color.FromArgb(245, 247, 250);
            Sidebar = Color.FromArgb(245, 247, 250);
            SidebarButton = Color.FromArgb(255, 255, 255);
            SidebarButtonHover = Color.FromArgb(239, 244, 251);
            SidebarButtonPressed = Color.FromArgb(226, 234, 246);
            Card = Color.FromArgb(255, 255, 255);
            CardAlt = Color.FromArgb(236, 241, 247);
            Line = Color.FromArgb(218, 225, 234);
            Text = Color.FromArgb(31, 41, 55);
            Subtle = Color.FromArgb(91, 103, 120);
            Muted = Color.FromArgb(135, 146, 160);
            Accent = Color.FromArgb(53, 111, 245);
            AccentHover = Color.FromArgb(70, 128, 255);
            AccentPressed = Color.FromArgb(35, 91, 220);
            Success = Color.FromArgb(22, 163, 74);
            Warning = Color.FromArgb(217, 119, 6);
            DisabledFill = Color.FromArgb(232, 237, 244);
            DisabledText = Color.FromArgb(145, 155, 170);
            CommandBar = Color.FromArgb(24, 26, 31);
            CommandButton = Color.FromArgb(39, 42, 50);
            CommandButtonHover = Color.FromArgb(56, 61, 72);
            CommandButtonPressed = Color.FromArgb(72, 79, 94);
            CommandButtonBorder = Color.FromArgb(70, 76, 88);
        }
    }
}
