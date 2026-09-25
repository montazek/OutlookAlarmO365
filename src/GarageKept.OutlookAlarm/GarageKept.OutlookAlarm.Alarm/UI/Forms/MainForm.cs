using System.Reflection;
using System.ComponentModel;
using System.Runtime.InteropServices;
using GarageKept.OutlookAlarm.Alarm.AlarmSources.Graph;
using GarageKept.OutlookAlarm.Alarm.Diagnostics;
using GarageKept.OutlookAlarm.Alarm.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace GarageKept.OutlookAlarm.Alarm.UI.Forms;

public partial class MainForm : BaseForm, IMainForm
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const int GwlExStyle = -20;
    private const long WsExTopmost = 0x00000008L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private const uint SwpNoSendChanging = 0x0400;

    private readonly Timer _slidingTimer = new() { Interval = 10 };
    private readonly Timer _topMostWatchdog = new() { Interval = 60000 };

    private bool _isExpanded;
    private bool _alarmManagerStarted;
    private bool _showingMicrosoft365Settings;
    private bool _signInPromptDismissed;

    public MainForm(ISettings settings, IAlarmManager alarmManager, IAlarmContainerControl containerControl) :
        base(true)
    {
        Settings = settings;
        AlarmManager = alarmManager;
        alarmManager.AlarmsUpdatedCallback += UpdateAlarms;

        InitializeComponent();
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        FormClosing += OnFormClosing;
        ContainerControl = containerControl;
        Controls.Add(ContainerControl as Control);

        SetupPosition();
        SetupContextMenu();

        // Subscribe to form's mouse enter and leave events
        MouseEnter += MainWindow_MouseEnter;
        MouseLeave += MainWindow_MouseLeave;
        // Subscribe to MouseEnter and MouseLeave events for each child control
        AddMouseEvents(this);

        // Initialize and set up the sliding timer
        _slidingTimer.Tick += SlidingTimer_Tick;
        _slidingTimer.Start();
        _topMostWatchdog.Tick += TopMostWatchdog_Tick;
        _topMostWatchdog.Start();

        AlarmManager = alarmManager;
        Shown += MainForm_Shown;

        var authentication = Program.ServiceProvider?.GetRequiredService<GraphAuthenticationService>();
        if (authentication is not null) authentication.SignInRequired += Authentication_SignInRequired;
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        ReassertTopMost("window shown");
        await EnsureMicrosoft365ConnectionAsync(showSettingsWhenRequired: true);
    }

    private void TopMostWatchdog_Tick(object? sender, EventArgs e)
    {
        // The managed TopMost property can remain true even when another topmost
        // application has moved above us. Reinsert this window at the top of the
        // topmost band without activating it or stealing keyboard focus.
        if (!_showingMicrosoft365Settings) ReassertTopMost("periodic watchdog");
    }

    private void ReassertTopMost(string reason)
    {
        if (IsDisposed || !IsHandleCreated) return;

        var extendedStyle = GetWindowLongPtr(Handle, GwlExStyle).ToInt64();
        var topMostStyleWasMissing = (extendedStyle & WsExTopmost) == 0;
        var succeeded = SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder | SwpNoSendChanging);

        if (!succeeded)
        {
            OutlookAlarmLog.Write($"Unable to reassert always-on-top ({reason}).",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
        else if (topMostStyleWasMissing)
        {
            OutlookAlarmLog.Write($"Recovered missing always-on-top status ({reason}).");
        }
    }

    private void Authentication_SignInRequired()
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(async () => await EnsureMicrosoft365ConnectionAsync(showSettingsWhenRequired: true));
    }

    private async Task<bool> EnsureMicrosoft365ConnectionAsync(bool showSettingsWhenRequired)
    {
        if (_showingMicrosoft365Settings) return false;

        var authentication = Program.ServiceProvider?.GetRequiredService<GraphAuthenticationService>();
        if (authentication is null) return false;

        string? account;
        try
        {
            account = await authentication.GetConnectedAccountAsync();
        }
        catch (Exception exception)
        {
            OutlookAlarmLog.Write("Unable to check Microsoft 365 sign-in.", exception);
            return false;
        }

        if (account is null && showSettingsWhenRequired && !_signInPromptDismissed)
        {
            _showingMicrosoft365Settings = true;
            try
            {
                var settingsForm = Program.ServiceProvider?.GetRequiredService<ISettingsForm>();
                if (settingsForm is not null)
                {
                    settingsForm.Owner = this;
                    settingsForm.ShowMicrosoft365Dialog();
                }
            }
            finally
            {
                _showingMicrosoft365Settings = false;
            }

            try
            {
                account = await authentication.GetConnectedAccountAsync();
            }
            catch (Exception exception)
            {
                OutlookAlarmLog.Write("Unable to verify Microsoft 365 sign-in after Settings closed.", exception);
                return false;
            }
            _signInPromptDismissed = account is null;
        }

        if (account is null) return false;
        _signInPromptDismissed = false;

        if (!_alarmManagerStarted)
        {
            AlarmManager.Start();
            _alarmManagerStarted = true;
        }
        else
        {
            AlarmManager.ForceFetch();
        }

        return true;
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SystemEvents_PowerModeChanged(sender, e));
            return;
        }

        switch (e.Mode)
        {
            case PowerModes.Suspend:
                // System is going to sleep, stop periodic calendar work until resume.
                RefreshTimer.Stop();
                AlarmManager.Stop();
                break;
            case PowerModes.Resume:
                // Restart both timers. The next fetch obtains a fresh Graph token/request
                // instead of relying on state that survived sleep.
                RefreshTimer.Start();
                if (_alarmManagerStarted)
                {
                    AlarmManager.Stop();
                    AlarmManager.Start();
                }
                _ = EnsureMicrosoft365ConnectionAsync(showSettingsWhenRequired: true);
                EnsureVisibleOnScreen();
                ReassertTopMost("resume from sleep");
                break;
            case PowerModes.StatusChange:
                break;
        }
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;

        BeginInvoke(() =>
        {
            EnsureVisibleOnScreen();
            ReassertTopMost("display settings changed");
        });
    }

    private ISettings Settings { get; }
    private IAlarmManager AlarmManager { get; }
    private IAlarmContainerControl ContainerControl { get; }

    public void UpdateAlarms(IEnumerable<IAlarm> alarms)
    {
        ContainerControl.Alarms = alarms;

        Invoke(()=>{

            EnsureVisibleOnScreen();

            CheckMouseLeaveForm();
        });
    }

    /// <summary>
    ///     Subscribes to MouseEnter and MouseLeave events for each child control.
    /// </summary>
    /// <param name="control">The control we are adding mouse events to.</param>
    private void AddMouseEvents(Control control)
    {
        control.MouseEnter -= ChildControl_MouseEnter;
        control.MouseEnter += ChildControl_MouseEnter;
        control.MouseLeave -= ChildControl_MouseLeave;
        control.MouseLeave += ChildControl_MouseLeave;

        foreach (Control child in control.Controls) AddMouseEvents(child);

        SetDraggable(this);
    }

    /// <summary>
    ///     Checks if the mouse pointer is still within the form bounds.
    /// </summary>
    private void CheckMouseLeaveForm()
    {
        var clientCursorPos = PointToClient(Cursor.Position);

        if (ClientRectangle.Contains(clientCursorPos) || rightClickMenu.Visible) return;

        _isExpanded = false;
        _slidingTimer.Start();
    }

    /// <summary>
    ///     Event handler for child control's MouseEnter event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void ChildControl_MouseEnter(object? sender, EventArgs e) { MainWindow_MouseEnter(sender, e); }

    /// <summary>
    ///     Event handler for child control's MouseLeave event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void ChildControl_MouseLeave(object? sender, EventArgs e) { CheckMouseLeaveForm(); }

    private void MainForm_Activated(object sender, EventArgs e)
    {
        EnsureVisibleOnScreen();
        ReassertTopMost("window activated");

        TopLevel = true;
    }

    /// <summary>
    ///     Event handler for the FormClosing event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        // Save the current position so we can restore it back to where it was on next run
        Settings.Main.Left = Location.X;
    }

    /// <summary>
    ///     Event handler for the MouseEnter event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void MainWindow_MouseEnter(object? sender, EventArgs e)
    {
        _isExpanded = true;
        _slidingTimer.Start();
    }

    /// <summary>
    ///     Event handler for the MouseLeave event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void MainWindow_MouseLeave(object? sender, EventArgs e)
    {
        if (rightClickMenu.Visible) return;

        _isExpanded = false;
        _slidingTimer.Start();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        var authentication = Program.ServiceProvider?.GetService<GraphAuthenticationService>();
        if (authentication is not null) authentication.SignInRequired -= Authentication_SignInRequired;
        _topMostWatchdog.Stop();
        _topMostWatchdog.Dispose();
        AlarmManager.Stop();
        AlarmManager.Dispose();
        ContainerControl.Dispose();
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
    }

    private void RefreshTimer_Tick(object sender, EventArgs e) { ContainerControl.RefreshTimer_Tick(sender, e); }

    private void RightClick_ResetAllAppointments(object? sender, EventArgs e) { AlarmManager.Reset(); }

    /// <summary>
    ///     Event handler for the About menu item click event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private static void RightClickMenu_AboutClick(object? sender, EventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        // Implement your About functionality here
        MessageBox.Show(@"Outlook Alarm by Garage Kept " + version);
    }

    /// <summary>
    ///     Event handler for the RefreshTimer menu item click event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void RightClickMenu_FetchNowClick(object? sender, EventArgs e) { AlarmManager.ForceFetch(); }

    /// <summary>
    ///     Event handler for the OutlookAlarmSettings menu item click event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private async void RightClickMenu_SettingsClick(object? sender, EventArgs e)
    {
        if (Program.ServiceProvider == null) return;

        var settingsForm = Program.ServiceProvider.GetRequiredService<ISettingsForm>();
        settingsForm.Owner = this;
        _showingMicrosoft365Settings = true;
        try
        {
            settingsForm.ShowDialog();
        }
        finally
        {
            _showingMicrosoft365Settings = false;
        }
        await EnsureMicrosoft365ConnectionAsync(showSettingsWhenRequired: false);
    }

    private void SetupContextMenu()
    {
        // Initialize and set up the context menu
        rightClickMenu.Items.Clear();
        rightClickMenu.Items.Add("Settings", null, RightClickMenu_SettingsClick);
        rightClickMenu.Items.Add("About", null, RightClickMenu_AboutClick);
        var advanced = new ToolStripMenuItem("Advanced");
        var advancedDropdown = new ToolStripDropDownMenu();
        var refresh = new ToolStripMenuItem("Fetch Now");
        refresh.Click += RightClickMenu_FetchNowClick;
        advancedDropdown.Items.Add(refresh);
        var reset = new ToolStripMenuItem("Reset All");
        reset.Click += RightClick_ResetAllAppointments;
        advancedDropdown.Items.Add(reset);
        advanced.DropDown = advancedDropdown;
        rightClickMenu.Items.Add(new ToolStripSeparator());
        rightClickMenu.Items.Add(advanced);
        rightClickMenu.Items.Add(new ToolStripSeparator());
        rightClickMenu.Items.Add("Close", null, (_, _) => Close());
    }

    private void SetupPosition()
    {
        // Set the form's start position to manual
        StartPosition = FormStartPosition.Manual;
        // Set the form's location and recover from a saved position that belonged
        // to a monitor which is no longer connected.
        Location = new Point(Settings.Main.Left, 0);
        EnsureVisibleOnScreen();
        // Save the form position when moved
        Move += (_, _) => { Settings.Main.Left = Left; };
    }

    /// <summary>
    ///     Event handler for the Timer Tick event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An EventArgs that contains the event data.</param>
    private void SlidingTimer_Tick(object? sender, EventArgs e)
    {
        var workingArea = GetCurrentScreen().WorkingArea;
        var targetY = _isExpanded
            ? workingArea.Top
            : workingArea.Top - Height + Settings.Main.BarSize;

        if (Math.Abs(Location.Y - targetY) <= 1)
        {
            Location = Location with { Y = targetY };
            _slidingTimer.Stop();
        }
        else
        {
            var step = (targetY - Location.Y) / Settings.Main.SliderSpeed;

            if (step == 0) step = targetY > Location.Y ? 1 : -1;

            Location = new Point(Location.X, Location.Y + step);
        }
    }

    private void EnsureVisibleOnScreen()
    {
        var workingArea = GetCurrentScreen().WorkingArea;
        var maximumLeft = Math.Max(workingArea.Left, workingArea.Right - Width);
        var left = Math.Clamp(Left, workingArea.Left, maximumLeft);
        var top = _isExpanded
            ? workingArea.Top
            : workingArea.Top - Height + Settings.Main.BarSize;

        Location = new Point(left, top);
    }

    private Screen GetCurrentScreen()
    {
        var width = Math.Max(Width, Settings.Main.MinimumWidth);
        var bestScreen = Screen.AllScreens
            .Select(screen => new
            {
                Screen = screen,
                Overlap = Math.Max(0,
                    Math.Min(Left + width, screen.WorkingArea.Right) -
                    Math.Max(Left, screen.WorkingArea.Left))
            })
            .OrderByDescending(candidate => candidate.Overlap)
            .FirstOrDefault();

        return bestScreen is { Overlap: > 0 }
            ? bestScreen.Screen
            : Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int width, int height, uint flags);
}
