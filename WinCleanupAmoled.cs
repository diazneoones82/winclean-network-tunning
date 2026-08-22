using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using Microsoft.Win32;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (!IsAdministrator())
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(Application.ExecutablePath);
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                startInfo.Arguments = BuildArguments(args);
                Process.Start(startInfo);
            }
            catch
            {
                MessageBox.Show("Administrator permission is required to run Windows cleanup commands.", "WinClean & Network Tunning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new CleanupForm(HasArg(args, "--autorun-system-cleanup")));
    }

    private static bool IsAdministrator()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool HasArg(string[] args, string value)
    {
        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string BuildArguments(string[] args)
    {
        List<string> values = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            values.Add("\"" + args[i].Replace("\"", "\\\"") + "\"");
        }
        return string.Join(" ", values.ToArray());
    }
}

internal sealed class CleanupForm : Form
{
    private const string AppName = "WinClean & Network Tunning";
    internal static readonly Color Amoled = Color.Black;
    internal static readonly Color Panel = Color.FromArgb(10, 7, 5);
    internal static readonly Color PanelSoft = Color.FromArgb(22, 12, 7);
    internal static readonly Color Accent = Color.FromArgb(249, 115, 22);
    internal static readonly Color AccentSoft = Color.FromArgb(194, 82, 15);
    internal static readonly Color TextMain = Color.FromArgb(255, 246, 235);
    internal static readonly Color TextMuted = Color.FromArgb(190, 172, 154);
    internal static readonly Color Warning = Color.FromArgb(255, 178, 92);
    private readonly List<CleanupStep> steps = new List<CleanupStep>();
    private readonly List<CleanupStep> activeSteps = new List<CleanupStep>();
    private readonly ListView stepList = new ListView();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly Label status = new Label();
    private readonly TextBox logBox = new TextBox();
    private readonly RoundedButton fullButton = new RoundedButton();
    private readonly RoundedButton networkButton = new RoundedButton();
    private readonly RoundedButton systemButton = new RoundedButton();
    private readonly RoundedButton tuningButton = new RoundedButton();
    private readonly RoundedButton dotNetButton = new RoundedButton();
    private readonly CheckBox autoRunSystemCheckBox = new CheckBox();
    private readonly NotifyIcon trayIcon = new NotifyIcon();
    private readonly ToolTip stepTip = new ToolTip();
    private string logPath = "";
    private volatile bool isRunning;
    private bool allowExit;

    public CleanupForm(bool autoRunSystemOnOpen)
    {
        BuildSteps();
        BuildUi();
        BuildTray();
        CheckDotNetRuntime();
        if (autoRunSystemOnOpen)
        {
            Shown += delegate { StartCleanup(StepGroup.System); };
        }
    }

    private void BuildUi()
    {
        Text = AppName;
        Width = 980;
        Height = 720;
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Amoled;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(10);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Resize += delegate { ApplyRoundedWindow(); };

        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.BackColor = Amoled;
        layout.ColumnCount = 1;
        layout.RowCount = 6;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
        Controls.Add(layout);
        ApplyRoundedWindow();

        Label title = new Label();
        title.Text = AppName;
        title.Font = new Font("Segoe UI Semibold", 24F);
        title.ForeColor = Accent;
        title.BackColor = Amoled;
        title.Dock = DockStyle.Fill;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Padding = new Padding(18, 0, 0, 0);
        layout.Controls.Add(title, 0, 0);

        status.Text = "Ready. A new log will be created for each run.";
        status.Dock = DockStyle.Fill;
        status.ForeColor = TextMuted;
        status.BackColor = Amoled;
        status.Padding = new Padding(18, 0, 18, 0);
        status.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(status, 0, 1);

        progress.Dock = DockStyle.Fill;
        progress.Minimum = 0;
        progress.Maximum = steps.Count;
        layout.Controls.Add(progress, 0, 2);

        TabControl actionTabs = new TabControl();
        actionTabs.Dock = DockStyle.Fill;
        actionTabs.BackColor = Amoled;
        actionTabs.Appearance = TabAppearance.FlatButtons;
        actionTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        actionTabs.ItemSize = new Size(132, 34);
        actionTabs.SizeMode = TabSizeMode.Fixed;
        actionTabs.DrawItem += DrawActionTab;
        actionTabs.SelectedIndexChanged += delegate
        {
            if (actionTabs.SelectedIndex == 0) ShowSteps(StepGroup.Full);
            if (actionTabs.SelectedIndex == 1) ShowSteps(StepGroup.Tuning);
        };

        TabPage cleanupTab = new TabPage("Cleanup");
        cleanupTab.BackColor = Amoled;
        FlowLayoutPanel cleanupActions = new FlowLayoutPanel();
        cleanupActions.Dock = DockStyle.Fill;
        cleanupActions.BackColor = Amoled;
        cleanupActions.Padding = new Padding(14, 10, 14, 8);
        cleanupActions.WrapContents = true;
        cleanupActions.Controls.Add(MakeActionButton(fullButton, "Full Cleanup", StepGroup.Full));
        cleanupActions.Controls.Add(MakeActionButton(networkButton, "Network Cleanup", StepGroup.Network));
        cleanupActions.Controls.Add(MakeActionButton(systemButton, "System Cleanup", StepGroup.System));
        cleanupActions.Controls.Add(MakeLinkButton(dotNetButton, "Get .NET"));
        autoRunSystemCheckBox.Text = "Run System Cleanup at startup without UAC prompt";
        autoRunSystemCheckBox.Width = 250;
        autoRunSystemCheckBox.Height = 36;
        autoRunSystemCheckBox.Margin = new Padding(12, 2, 10, 2);
        autoRunSystemCheckBox.ForeColor = TextMuted;
        autoRunSystemCheckBox.BackColor = Amoled;
        autoRunSystemCheckBox.Checked = IsStartupEnabled();
        autoRunSystemCheckBox.CheckedChanged += delegate { SetStartupEnabled(autoRunSystemCheckBox.Checked); };
        cleanupActions.Controls.Add(autoRunSystemCheckBox);
        cleanupTab.Controls.Add(cleanupActions);
        actionTabs.TabPages.Add(cleanupTab);

        TabPage tuningTab = new TabPage("Tunning");
        tuningTab.BackColor = Amoled;
        FlowLayoutPanel tuningActions = new FlowLayoutPanel();
        tuningActions.Dock = DockStyle.Fill;
        tuningActions.BackColor = Amoled;
        tuningActions.Padding = new Padding(14, 10, 14, 8);
        tuningActions.WrapContents = true;
        tuningActions.Controls.Add(MakeActionButton(tuningButton, "Run Tunning", StepGroup.Tuning));
        tuningTab.Controls.Add(tuningActions);
        actionTabs.TabPages.Add(tuningTab);

        layout.Controls.Add(actionTabs, 0, 5);

        logBox.Dock = DockStyle.Fill;
        logBox.Multiline = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.ReadOnly = true;
        logBox.BackColor = Panel;
        logBox.ForeColor = Color.FromArgb(255, 198, 130);
        logBox.BorderStyle = BorderStyle.FixedSingle;
        BorderPanel logPanel = new BorderPanel();
        logPanel.Dock = DockStyle.Fill;
        logPanel.Padding = new Padding(8);
        logPanel.Controls.Add(logBox);
        layout.Controls.Add(logPanel, 0, 4);

        stepList.Dock = DockStyle.Fill;
        stepList.View = View.Details;
        stepList.FullRowSelect = true;
        stepList.GridLines = false;
        stepList.BackColor = Panel;
        stepList.ForeColor = TextMain;
        stepList.BorderStyle = BorderStyle.None;
        stepList.HideSelection = false;
        stepList.Columns.Add("#", 46);
        stepList.Columns.Add("Status", 110);
        stepList.Columns.Add("Command", 760);
        stepList.MouseClick += ShowStepInfo;
        BorderPanel listPanel = new BorderPanel();
        listPanel.Dock = DockStyle.Fill;
        listPanel.Padding = new Padding(8);
        listPanel.Controls.Add(stepList);
        layout.Controls.Add(listPanel, 0, 3);

        ShowSteps(StepGroup.Full);
    }

    private void BuildTray()
    {
        bool lightTheme = IsWindowsLightTheme();
        Color menuBack = lightTheme ? Color.FromArgb(255, 250, 245) : Panel;
        Color menuText = lightTheme ? Color.FromArgb(58, 33, 18) : TextMain;
        Color menuSelected = lightTheme ? Color.FromArgb(255, 228, 204) : Color.FromArgb(62, 29, 10);

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.BackColor = menuBack;
        menu.ForeColor = menuText;
        menu.Font = new Font("Segoe UI", 9.5F);
        menu.Padding = new Padding(7);
        menu.ShowImageMargin = false;
        menu.Renderer = new TrayMenuRenderer(menuBack, menuText, menuSelected, Accent);
        menu.Opening += delegate
        {
            menu.BeginInvoke(new MethodInvoker(delegate
            {
                using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, menu.Width, menu.Height), 14))
                {
                    menu.Region = new Region(path);
                }
            }));
        };

        AddTrayItem(menu, "Open", delegate { RestoreFromTray(); });
        menu.Items.Add(new ToolStripSeparator());
        AddTrayItem(menu, "Run Full Cleanup", delegate { StartFromTray(StepGroup.Full); });
        AddTrayItem(menu, "Run Network Cleanup", delegate { StartFromTray(StepGroup.Network); });
        AddTrayItem(menu, "Run System Cleanup", delegate { StartFromTray(StepGroup.System); });
        AddTrayItem(menu, "Run Tunning", delegate { StartFromTray(StepGroup.Tuning); });
        menu.Items.Add(new ToolStripSeparator());
        AddTrayItem(menu, "Exit", delegate { ExitFromTray(); });

        trayIcon.Icon = Icon == null ? SystemIcons.Application : Icon;
        trayIcon.Text = AppName;
        trayIcon.ContextMenuStrip = menu;
        trayIcon.Visible = true;
        trayIcon.DoubleClick += delegate { RestoreFromTray(); };
    }

    private void AddTrayItem(ContextMenuStrip menu, string text, EventHandler click)
    {
        ToolStripMenuItem item = new ToolStripMenuItem(text);
        item.ForeColor = menu.ForeColor;
        item.BackColor = menu.BackColor;
        item.AutoSize = false;
        item.Width = 210;
        item.Height = 34;
        item.Click += click;
        menu.Items.Add(item);
    }

    private bool IsWindowsLightTheme()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                return value is int && (int)value != 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private void BuildSteps()
    {
        Add(StepGroup.Network, "Flush DNS resolver cache", "ipconfig /flushdns");
        Add(StepGroup.Network, "Purge NetBIOS name cache", "nbtstat -R");
        Add(StepGroup.Network, "Release existing IP address leases", "ipconfig /release");
        Add(StepGroup.Network, "Renew IP addresses from DHCP", "ipconfig /renew");
        Add(StepGroup.Network, "Clear ARP cache", "arp -d *");
        Add(StepGroup.Network, "Reset Winsock catalog", "netsh winsock reset");
        Add(StepGroup.Network, "Reset TCP/IP stack", "netsh int ip reset");
        Add(StepGroup.Network, "Clear persistent static routes", "route -f");
        Add(StepGroup.Network, "Reset Windows Defender Firewall", "netsh advfirewall reset");
        Add(StepGroup.Network, "Flush local BranchCache data", "netsh branchcache reset");
        Add(StepGroup.System, "Clear user temp files", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:TEMP '*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear system temp files", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path 'C:\\Windows\\Temp\\*' -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Windows Prefetch files", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path 'C:\\Windows\\Prefetch\\*' -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Cleanup old Windows Update components", "Dism.exe /online /Cleanup-Image /StartComponentCleanup /ResetBase");
        Add(StepGroup.System, "Stop Windows Update and BITS services", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Stop-Service -Name wuauserv,bits -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Windows Update stage download cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path 'C:\\Windows\\SoftwareDistribution\\Download\\*' -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Start Windows Update and BITS services", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Start-Service -Name wuauserv,bits -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Delivery Optimization peer-to-peer cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:ProgramData 'Microsoft\\Windows\\SoftwareDistribution\\DeliveryOptimization\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Delete oldest System Restore shadow copy", "vssadmin list shadows /for=C: | findstr /C:\"Shadow Copy ID:\" >nul && vssadmin delete shadows /for=C: /oldest /quiet || exit /b 0");
        Add(StepGroup.System, "Clear Chrome browser cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:LocalAppData 'Google\\Chrome\\User Data\\Default\\Cache\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Chrome code cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:LocalAppData 'Google\\Chrome\\User Data\\Default\\Code Cache\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Chrome GPU cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:LocalAppData 'Google\\Chrome\\User Data\\Default\\GPUCache\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Edge browser cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:LocalAppData 'Microsoft\\Edge\\User Data\\Default\\Cache\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Edge code cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:LocalAppData 'Microsoft\\Edge\\User Data\\Default\\Code Cache\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Edge GPU cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:LocalAppData 'Microsoft\\Edge\\User Data\\Default\\GPUCache\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Firefox cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -Path (Join-Path $env:LocalAppData 'Mozilla\\Firefox\\Profiles') -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -Path (Join-Path $_.FullName 'cache2\\*') -Recurse -Force -ErrorAction SilentlyContinue }\"");
        Add(StepGroup.System, "Clear Firefox startup cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -Path (Join-Path $env:LocalAppData 'Mozilla\\Firefox\\Profiles') -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -Path (Join-Path $_.FullName 'startupCache\\*') -Recurse -Force -ErrorAction SilentlyContinue }\"");
        Add(StepGroup.System, "Clear Floorp cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -Path (Join-Path $env:LocalAppData 'Floorp\\Profiles') -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -Path (Join-Path $_.FullName 'cache2\\*') -Recurse -Force -ErrorAction SilentlyContinue }\"");
        Add(StepGroup.System, "Clear Floorp startup cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -Path (Join-Path $env:LocalAppData 'Floorp\\Profiles') -Directory -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -Path (Join-Path $_.FullName 'startupCache\\*') -Recurse -Force -ErrorAction SilentlyContinue }\"");
        Add(StepGroup.System, "Clear Brave browser cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:LocalAppData 'BraveSoftware\\Brave-Browser\\User Data\\Default\\Cache\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.System, "Clear Brave code cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:LocalAppData 'BraveSoftware\\Brave-Browser\\User Data\\Default\\Code Cache\\*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Network, "Flush DNS resolver cache again", "ipconfig /flushdns");
        Add(StepGroup.System, "Reset Windows Store cache", "wsreset.exe");

        Add(StepGroup.Tuning, "Stop telemetry, diagnostics, tracking, and SysMain services", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Stop-Service -Name DiagTrack,dmwappushservice,sysmain -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Disable telemetry, diagnostics, tracking, and SysMain startup", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"'DiagTrack','dmwappushservice','sysmain' | ForEach-Object { Set-Service -Name $_ -StartupType Disabled -ErrorAction SilentlyContinue }\"");
        Add(StepGroup.Tuning, "Block Windows telemetry data collection", "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v \"AllowTelemetry\" /t REG_DWORD /d 0 /f");
        Add(StepGroup.Tuning, "Disable Application Compatibility telemetry", "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat\" /v \"AITEnable\" /t REG_DWORD /d 0 /f");
        Add(StepGroup.Tuning, "Disable Windows CEIP telemetry", "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\SQMClient\\Windows\" /v \"CEIPEnable\" /t REG_DWORD /d 0 /f");
        Add(StepGroup.Tuning, "Disable Compatibility Appraiser scheduled task", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Disable-ScheduledTask -TaskPath '\\Microsoft\\Windows\\Application Experience\\' -TaskName 'Microsoft Compatibility Appraiser' -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Disable ProgramDataUpdater scheduled task", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Disable-ScheduledTask -TaskPath '\\Microsoft\\Windows\\Application Experience\\' -TaskName 'ProgramDataUpdater' -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Disable CEIP Consolidator scheduled task", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Disable-ScheduledTask -TaskPath '\\Microsoft\\Windows\\Customer Experience Improvement Program\\' -TaskName 'Consolidator' -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Disable automatic pagefile management", "wmic computersystem where name=\"%computername%\" set AutomaticManagedPagefile=False");
        Add(StepGroup.Tuning, "Set static C drive pagefile to 8192 MB", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$name='C:\\pagefile.sys'; $pf=Get-WmiObject -Class Win32_PageFileSetting -ErrorAction SilentlyContinue | Where-Object { $_.Name -ieq $name } | Select-Object -First 1; if($pf){ $pf.InitialSize=8192; $pf.MaximumSize=8192; $pf.Put() | Out-Null } else { Set-WmiInstance -Class Win32_PageFileSetting -Arguments @{Name=$name;InitialSize=8192;MaximumSize=8192} | Out-Null }\"");
        Add(StepGroup.Tuning, "Eliminate menu popup delay", "reg add \"HKCU\\Control Panel\\Desktop\" /v \"MenuShowDelay\" /t REG_SZ /d \"0\" /f");
        Add(StepGroup.Tuning, "Terminate unresponsive apps faster during shutdown", "reg add \"HKCU\\Control Panel\\Desktop\" /v \"WaitToKillAppTimeout\" /t REG_SZ /d \"2000\" /f");
        Add(StepGroup.Tuning, "Lower hung app timeout", "reg add \"HKCU\\Control Panel\\Desktop\" /v \"HungAppTimeout\" /t REG_SZ /d \"1000\" /f");
        Add(StepGroup.Tuning, "Disable minimize and maximize animations", "reg add \"HKCU\\Control Panel\\Desktop\\WindowMetrics\" /v \"MinAnimate\" /t REG_SZ /d \"0\" /f");
        Add(StepGroup.Tuning, "Unlock Ultimate Performance power scheme", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"if(-not (powercfg /L | Select-String 'Ultimate Performance')){ powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 | Out-Null }\"");
        Add(StepGroup.Tuning, "Activate Ultimate Performance power scheme", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$scheme=powercfg /L | Select-String 'Ultimate Performance' | Select-Object -First 1; if($scheme -and $scheme.Line -match '([0-9a-fA-F-]{36})'){ powercfg /setactive $matches[1] }\"");
        Add(StepGroup.Tuning, "Enable TCP auto tuning", "netsh int tcp set global autotuninglevel=normal");
        Add(StepGroup.Tuning, "Set CTCP congestion provider", "netsh int tcp set supplemental template=internet congestionprovider=ctcp");
        Add(StepGroup.Tuning, "Disable Receive Segment Coalescing", "netsh int tcp set global rsc=disabled");
        Add(StepGroup.Tuning, "Enable hardware task offloading", "netsh int ip set global taskoffload=enabled");
        Add(StepGroup.Tuning, "Flush DNS resolver cache", "ipconfig /flushdns");
        Add(StepGroup.Tuning, "Reset Winsock catalog", "netsh winsock reset");
        Add(StepGroup.Tuning, "Clear user temp files", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path (Join-Path $env:TEMP '*') -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Clear system temp files", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path 'C:\\Windows\\Temp\\*' -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Clear Windows Prefetch files", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path 'C:\\Windows\\Prefetch\\*' -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Stop Windows Update and BITS services", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Stop-Service -Name wuauserv,bits -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Clear Windows Update stage download cache", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path 'C:\\Windows\\SoftwareDistribution\\Download\\*' -Recurse -Force -ErrorAction SilentlyContinue\"");
        Add(StepGroup.Tuning, "Start Windows Update and BITS services", "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Start-Service -Name wuauserv,bits -ErrorAction SilentlyContinue\"");
    }

    private void Add(StepGroup group, string name, string command)
    {
        steps.Add(new CleanupStep(group, name, command));
    }

    private RoundedButton MakeActionButton(RoundedButton button, string text, StepGroup group)
    {
        button.Text = text;
        button.Width = 210;
        button.Height = 36;
        button.Margin = new Padding(4, 2, 10, 2);
        button.BackColor = PanelSoft;
        button.ForeColor = Color.FromArgb(255, 210, 166);
        button.BorderColor = Accent;
        button.Font = new Font("Segoe UI Semibold", 11F);
        button.Click += delegate { StartCleanup(group); };
        return button;
    }

    private RoundedButton MakeLinkButton(RoundedButton button, string text)
    {
        button.Text = text;
        button.Width = 120;
        button.Height = 36;
        button.Margin = new Padding(4, 2, 10, 2);
        button.BackColor = PanelSoft;
        button.ForeColor = Color.FromArgb(255, 210, 166);
        button.BorderColor = AccentSoft;
        button.Font = new Font("Segoe UI Semibold", 11F);
        button.Click += delegate { OpenDotNetDownload(); };
        return button;
    }

    private void CheckDotNetRuntime()
    {
        int release = GetNetFrameworkRelease();
        if (release > 0 && release < 528040)
        {
            DialogResult result = MessageBox.Show(
                ".NET Framework is present, but an older release was detected. Open the official Microsoft .NET Framework download page?",
                AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (result == DialogResult.Yes) OpenDotNetDownload();
        }
    }

    private int GetNetFrameworkRelease()
    {
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
            {
                object value = key == null ? null : key.GetValue("Release");
                return value is int ? (int)value : 0;
            }
        }
        catch
        {
            return 0;
        }
    }

    private void OpenDotNetDownload()
    {
        Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48") { UseShellExecute = true });
    }

    private bool IsStartupEnabled()
    {
        return RunHidden("schtasks.exe", "/Query /TN \"WinCleanNetworkTunning System Cleanup\"").ExitCode == 0;
    }

    private void SetStartupEnabled(bool enabled)
    {
        if (enabled)
        {
            string taskCommand = "\"" + Application.ExecutablePath + "\" --autorun-system-cleanup";
            string args = "/Create /F /TN \"WinCleanNetworkTunning System Cleanup\" /SC ONLOGON /RL HIGHEST /TR \"" + taskCommand + "\"";
            CommandResult result = RunHidden("schtasks.exe", args);
            if (result.ExitCode == 0)
            {
                RemoveOldRegistryStartup();
                status.Text = "Startup enabled: System Cleanup will auto-run elevated at sign-in.";
            }
            else
            {
                status.Text = "Could not create startup task: " + Shorten(result.Text, 180);
                autoRunSystemCheckBox.Checked = false;
            }
            return;
        }

        CommandResult deleteResult = RunHidden("schtasks.exe", "/Delete /F /TN \"WinCleanNetworkTunning System Cleanup\"");
        RemoveOldRegistryStartup();
        status.Text = deleteResult.ExitCode == 0 ? "Startup disabled." : "Startup was not enabled.";
    }

    private void RemoveOldRegistryStartup()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null) key.DeleteValue("WinCleanNetworkTunning", false);
            }
        }
        catch { }
    }

    private CommandResult RunHidden(string fileName, string arguments)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(fileName, arguments);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new CommandResult(process.ExitCode, (output + error).Trim());
        }
    }

    private void DrawActionTab(object sender, DrawItemEventArgs e)
    {
        TabControl tabs = (TabControl)sender;
        bool selected = e.Index == tabs.SelectedIndex;
        Rectangle rect = e.Bounds;
        rect.Inflate(-3, -3);

        using (GraphicsPath path = RoundedRect(rect, 10))
        using (SolidBrush fill = new SolidBrush(selected ? Accent : PanelSoft))
        using (Pen border = new Pen(selected ? Accent : AccentSoft, 1.2F))
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }

        Color textColor = selected ? Color.Black : Color.FromArgb(255, 210, 166);
        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, Font, rect, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void ApplyRoundedWindow()
    {
        using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), 18))
        {
            Region = new Region(path);
        }
    }

    internal static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter - 1, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter - 1, bounds.Bottom - diameter - 1, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter - 1, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ShowSteps(StepGroup group)
    {
        if (isRunning) return;
        activeSteps.Clear();
        for (int i = 0; i < steps.Count; i++)
        {
            if ((group == StepGroup.Full && steps[i].Group != StepGroup.Tuning) || steps[i].Group == group)
            {
                steps[i].ExitCode = null;
                steps[i].LastLog = "";
                activeSteps.Add(steps[i]);
            }
        }

        stepList.BeginUpdate();
        stepList.Items.Clear();
        for (int i = 0; i < activeSteps.Count; i++)
        {
            ListViewItem item = new ListViewItem((i + 1).ToString());
            item.SubItems.Add("Pending");
            item.SubItems.Add(activeSteps[i].Name);
            stepList.Items.Add(item);
        }
        if (stepList.Items.Count > 0) stepList.TopItem = stepList.Items[0];
        stepList.EndUpdate();
        progress.Maximum = Math.Max(1, activeSteps.Count);
        progress.Value = 0;
    }

    private void StartCleanup(StepGroup group)
    {
        if (isRunning)
        {
            trayIcon.ShowBalloonTip(2500, AppName, "A cleanup run is already in progress.", ToolTipIcon.Info);
            return;
        }
        ShowSteps(group);
        logPath = NewLogPath();
        logBox.Clear();
        SetStatus("Starting. Log: " + logPath);
        isRunning = true;
        SetButtons(false);
        Thread worker = new Thread(RunCleanup);
        worker.IsBackground = true;
        worker.Start();
    }

    private void StartFromTray(StepGroup group)
    {
        StartCleanup(group);
        trayIcon.ShowBalloonTip(2500, AppName, "Started " + GroupLabel(group) + " in the background.", ToolTipIcon.Info);
    }

    private void RunCleanup()
    {
        File.WriteAllText(logPath, AppName + " started " + DateTime.Now + Environment.NewLine);

        for (int i = 0; i < activeSteps.Count; i++)
        {
            CleanupStep step = activeSteps[i];
            UpdateStep(i, "Running", Accent);
            SetStatus("Step " + (i + 1) + " of " + activeSteps.Count + ": " + step.Name);
            AppendLog("Running: " + step.Command);
            CommandResult result = RunCommand(step.Command);
            step.ExitCode = result.ExitCode;
            step.LastLog = result.Text;

            if (result.ExitCode == 0)
            {
                UpdateStep(i, "Done", TextMain);
            }
            else
            {
                UpdateStep(i, "Exit " + result.ExitCode, Warning);
            }

            SetProgress(i + 1);
        }

        SetStatus("Complete. Some network reset changes may require a restart. Log: " + logPath);
        AppendLog("Complete.");
        Invoke(new Action(delegate
        {
            trayIcon.ShowBalloonTip(3500, AppName, "Run complete. Log: " + Path.GetFileName(logPath), ToolTipIcon.Info);
        }));
        isRunning = false;
        SetButtons(true);
    }

    private CommandResult RunCommand(string command)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            string text = (output + error).Trim();
            File.AppendAllText(logPath, "[" + DateTime.Now + "] " + command + Environment.NewLine + output + error + Environment.NewLine);
            if (output.Length > 0) AppendLog(output.Trim());
            if (error.Length > 0) AppendLog(error.Trim());
            return new CommandResult(process.ExitCode, text);
        }
    }

    private string NewLogPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinCleanup-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + ".log");
    }

    private void UpdateStep(int index, string text, Color color)
    {
        Invoke(new Action(delegate
        {
            stepList.BeginUpdate();
            stepList.Items[index].SubItems[1].Text = text;
            stepList.Items[index].ForeColor = color;
            stepList.SelectedItems.Clear();
            stepList.Items[index].Selected = true;
            stepList.TopItem = stepList.Items[Math.Max(0, index - 4)];
            stepList.EndUpdate();
        }));
    }

    private void ShowStepInfo(object sender, MouseEventArgs e)
    {
        ListViewItem item = stepList.GetItemAt(e.X, e.Y);
        if (item == null) return;

        CleanupStep step = activeSteps[item.Index];
        string state = item.SubItems[1].Text;
        string detail = "Pending";
        if (step.ExitCode.HasValue)
        {
            if (step.ExitCode.Value == 0)
            {
                detail = "Completed successfully.";
            }
            else if (step.LastLog.Length > 0)
            {
                detail = "Exit " + step.ExitCode.Value + ": " + Shorten(step.LastLog, 900);
            }
            else
            {
                detail = "Exit " + step.ExitCode.Value + ": Windows returned a non-zero result without extra output. Check the log for this command.";
            }
        }

        string message = step.Name + Environment.NewLine + "State: " + state + Environment.NewLine + Environment.NewLine + detail;
        stepTip.Show(message, stepList, e.X + 16, e.Y + 16, 5500);
    }

    private string Shorten(string text, int maxLength)
    {
        text = text.Replace("\r", "").Trim();
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + "...";
    }

    private void SetStatus(string text)
    {
        Invoke(new Action(delegate { status.Text = text; }));
    }

    private void SetProgress(int value)
    {
        Invoke(new Action(delegate { progress.Value = value; }));
    }

    private void AppendLog(string text)
    {
        if (text.Length == 0) return;
        Invoke(new Action(delegate
        {
            logBox.AppendText(text + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }));
    }

    private void SetButtons(bool enabled)
    {
        Invoke(new Action(delegate
        {
            fullButton.Enabled = enabled;
            networkButton.Enabled = enabled;
            systemButton.Enabled = enabled;
            tuningButton.Enabled = enabled;
        }));
    }

    private string GroupLabel(StepGroup group)
    {
        if (group == StepGroup.Network) return "Network Cleanup";
        if (group == StepGroup.System) return "System Cleanup";
        if (group == StepGroup.Tuning) return "Tunning";
        return "Full Cleanup";
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        trayIcon.ShowBalloonTip(2500, AppName, "Still running in the background. Use the tray icon to open, run, or exit.", ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        if (isRunning && MessageBox.Show("A run is still in progress. Exit anyway?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        allowExit = true;
        trayIcon.Visible = false;
        Close();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyRoundedWindow();
        if (WindowState == FormWindowState.Minimized) HideToTray();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!allowExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        trayIcon.Visible = false;
        trayIcon.Dispose();
        base.OnFormClosing(e);
    }
}

internal enum StepGroup
{
    Full,
    Network,
    System,
    Tuning
}

internal sealed class RoundedButton : Button
{
    public Color BorderColor = CleanupForm.Accent;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
        Color fill = Enabled ? BackColor : Color.FromArgb(16, 12, 10);
        Color border = Enabled ? BorderColor : Color.FromArgb(75, 45, 25);
        Color text = Enabled ? ForeColor : Color.FromArgb(110, 92, 78);

        using (GraphicsPath path = CleanupForm.RoundedRect(rect, 14))
        using (SolidBrush brush = new SolidBrush(fill))
        using (Pen pen = new Pen(border, 1.4F))
        using (StringFormat format = new StringFormat())
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            using (SolidBrush textBrush = new SolidBrush(text))
            {
                e.Graphics.DrawString(Text, Font, textBrush, rect, format);
            }
        }
    }
}

internal sealed class BorderPanel : Panel
{
    private readonly Color borderColor = CleanupForm.AccentSoft;

    public BorderPanel()
    {
        BackColor = CleanupForm.Amoled;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = CleanupForm.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 14))
        using (Pen pen = new Pen(borderColor, 1.2F))
        {
            e.Graphics.DrawPath(pen, path);
        }
    }
}

internal sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly Color background;
    private readonly Color text;
    private readonly Color selected;
    private readonly Color accent;

    public TrayMenuRenderer(Color background, Color text, Color selected, Color accent)
    {
        this.background = background;
        this.text = text;
        this.selected = selected;
        this.accent = accent;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(background);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        Rectangle rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
        if (e.Item.Selected || e.Item.Pressed)
        {
            using (GraphicsPath path = CleanupForm.RoundedRect(rect, 9))
            using (SolidBrush brush = new SolidBrush(selected))
            using (Pen pen = new Pen(accent, 1F))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = text;
        e.TextRectangle = new Rectangle(e.TextRectangle.X + 4, e.TextRectangle.Y, e.TextRectangle.Width - 4, e.TextRectangle.Height);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using (Pen pen = new Pen(Color.FromArgb(150, accent), 1F))
        {
            e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }
    }
}

internal sealed class CleanupStep
{
    public readonly StepGroup Group;
    public readonly string Name;
    public readonly string Command;
    public int? ExitCode;
    public string LastLog = "";

    public CleanupStep(StepGroup group, string name, string command)
    {
        Group = group;
        Name = name;
        Command = command;
    }
}

internal sealed class CommandResult
{
    public readonly int ExitCode;
    public readonly string Text;

    public CommandResult(int exitCode, string text)
    {
        ExitCode = exitCode;
        Text = text;
    }
}
