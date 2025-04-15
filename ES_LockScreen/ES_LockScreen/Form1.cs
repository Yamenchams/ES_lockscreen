using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Data.SqlClient;
using Microsoft.Win32;
using System.Diagnostics;
using System.Collections.Generic;

//SAMIR
namespace ES_LockScreen
{
    public partial class Form1 : Form
    {
        string cardBuffer = "";
        bool isProcessing = false;
        Label infoLabel;
        private List<DateTime> keyPressTimes = new List<DateTime>();
        private const int CardReaderThresholdMs = 50; // Threshold in milliseconds, adjust based on testing

        private Timer swipeTimeoutTimer = new Timer();

        public Form1()
        {
            InitializeComponent();
            SetupForm();
            BuildUI();

            swipeTimeoutTimer.Interval = 1000; // a second of inactivity
            swipeTimeoutTimer.Tick += SwipeTimeoutTimer_Tick;

        }
        private void BuildUI()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.Black;
            layout.ColumnCount = 1;
            layout.RowCount = 5;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

            // Headline
            Label headline = new Label();
            headline.Text = "🔒 Welcome to Ericsson Space";
            headline.ForeColor = Color.White;
            headline.Font = new Font("Segoe UI", 36, FontStyle.Bold);
            headline.TextAlign = ContentAlignment.MiddleCenter;
            headline.Dock = DockStyle.Fill;
            layout.Controls.Add(headline, 0, 0);

            // Instruction
            Label instructions = new Label();
            instructions.Text = "📋 Please swipe your BTH card to access this PC.";
            instructions.ForeColor = Color.White;
            instructions.Font = new Font("Segoe UI", 24, FontStyle.Regular);
            instructions.TextAlign = ContentAlignment.MiddleCenter;
            instructions.Dock = DockStyle.Fill;
            layout.Controls.Add(instructions, 0, 1);

            // Spacer
            Label spacer = new Label();
            spacer.Dock = DockStyle.Fill;
            layout.Controls.Add(spacer, 0, 2);

            // Contact
            Label contact = new Label();
            contact.Text = "❓ If you don't have access, contact:\nUsman Nasir\nusman.nasir@bth.se";
            contact.ForeColor = Color.White;
            contact.Font = new Font("Segoe UI", 20, FontStyle.Regular);
            contact.TextAlign = ContentAlignment.MiddleCenter;
            contact.Dock = DockStyle.Fill;
            layout.Controls.Add(contact, 0, 3);

            // Status
            infoLabel = new Label();
            infoLabel.ForeColor = Color.LimeGreen;
            infoLabel.Font = new Font("Segoe UI", 28, FontStyle.Bold);
            infoLabel.TextAlign = ContentAlignment.MiddleCenter;
            infoLabel.Dock = DockStyle.Fill;
            layout.Controls.Add(infoLabel, 0, 4);

            this.Controls.Add(layout);
        }

        private void SwipeTimeoutTimer_Tick(object sender, EventArgs e)
        {
            swipeTimeoutTimer.Stop();

            if (cardBuffer.Length > 0 && !isProcessing)
            {
                ProcessCard();
            }
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.ShowInTaskbar = false;
            this.ControlBox = false;
            Cursor.Hide();
            HideTaskbar();
            DisableTaskManager();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ForceFocus();
            System.Threading.Thread.Sleep(100); // short delay
            InitializeKeyboardHook();
        }

        private void ForceFocus()
        {
            this.TopMost = true;
            this.WindowState = FormWindowState.Normal;
            this.WindowState = FormWindowState.Maximized;
            this.Focus();
            this.BringToFront();
            this.Activate();
            this.Select();
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc;
        private static IntPtr _hookID = IntPtr.Zero;

        private void InitializeKeyboardHook()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Block Alt+F4
            if (keyData == (Keys.Alt | Keys.F4))
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            EnableTaskManager();
            ShowTaskbar();
            Cursor.Show();
            swipeTimeoutTimer?.Stop();
            UnhookWindowsHookEx(_hookID);

            base.OnFormClosing(e); // keep this — it ensures Windows properly closes the form
        }


        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                keyPressTimes.Add(DateTime.Now); // Record the time of the key press
                // Block Alt+F4, Alt+Tab, Ctrl+Esc, Win keys
                bool isAltDown = (Control.ModifierKeys & Keys.Alt) == Keys.Alt;
                bool isCtrlDown = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                bool isShiftDown = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

                if ((isAltDown && vkCode == (int)Keys.F4) ||
                    (isAltDown && vkCode == (int)Keys.Tab) ||
                    (isCtrlDown && vkCode == (int)Keys.Escape) ||
                    (vkCode == (int)Keys.LWin) ||
                    (vkCode == (int)Keys.RWin))
                {
                    return (IntPtr)1; // Block it
                }



                // Enter = process card
                if (vkCode == (int)Keys.Enter)
                {
                    swipeTimeoutTimer.Stop();
                    swipeTimeoutTimer.Start(); // Wait for timeout to process
                    return (IntPtr)1;
                }

                // Allow digits 0–9
                if (vkCode >= 0x30 && vkCode <= 0x39)
                {
                    cardBuffer += (char)vkCode;
                    swipeTimeoutTimer.Stop();
                    swipeTimeoutTimer.Start();
                    return (IntPtr)1;
                }

                // Block everything else
                return (IntPtr)1;
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;

            if (m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt32() == SC_CLOSE))
            {
                // Block system close commands (e.g., from Alt+Tab thumbnail)
                return;
            }

            base.WndProc(ref m);
        }


        private void ProcessCard()
        {
            if (isProcessing) return;
            isProcessing = true;

            bool isCardReader = IsCardReaderInput();
            keyPressTimes.Clear(); // Reset for the next input

            Invoke((MethodInvoker)delegate
            {
                var (isAuthorized, message, color) = CheckCardInDatabase(cardBuffer, isCardReader);

                infoLabel.ForeColor = color == ConsoleColor.Green ? Color.LimeGreen :
                                     color == ConsoleColor.Red ? Color.Red :
                                     Color.Yellow;
                infoLabel.Text = message;
                LogSwipe(cardBuffer, message);
                Application.DoEvents();
                System.Threading.Thread.Sleep(2000);

                if (isAuthorized)
                {
                    // Cleanup and exit
                    EnableTaskManager();
                    ShowTaskbar();
                    Cursor.Show();
                    UnhookWindowsHookEx(_hookID);
                    swipeTimeoutTimer.Stop();
                    this.Close();
                }
                else
                {
                    // Reset if access denied
                    infoLabel.Text = "";
                    cardBuffer = "";
                    isProcessing = false;
                }
            });
        }
        private bool IsCardReaderInput()
        {
            if (keyPressTimes.Count < 2) return false; // Need at least 2 key presses to compare

            for (int i = 1; i < keyPressTimes.Count; i++)
            {
                TimeSpan timeDiff = keyPressTimes[i] - keyPressTimes[i - 1];
                if (timeDiff.TotalMilliseconds > CardReaderThresholdMs)
                {
                    return false; // If any gap is too long, it's not a card reader
                }
            }
            return true; // All gaps are short, likely a card reader
        }

        private (bool isAuthorized, string message, ConsoleColor color) CheckCardInDatabase(string cardNumber, bool isCardReader)
        {
            if (cardNumber == "1234")
            {
                return (true, "Acress Granted - Debug Code", ConsoleColor.Green);
            }
            string connectionString = "Server=admin-panel-server.database.windows.net;Database=admin_panel_db;User ID=sqladmin@admin-panel-server;Password=Card.1111;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    // Check user_id only if input is from a card reader
                    if (isCardReader)
                    {
                        string userIdQuery = "SELECT COUNT(*) FROM Users WHERE user_id = @card AND status2 = '1'";
                        SqlCommand userIdCmd = new SqlCommand(userIdQuery, conn);
                        userIdCmd.Parameters.AddWithValue("@card", cardNumber);
                        int count = (int)userIdCmd.ExecuteScalar();

                        if (count > 0)
                        {
                            return (true, $"✅ Access Granted for user_id: {cardNumber}", ConsoleColor.Green);
                        }
                    }

                    // Check random_code (allowed from any input source)
                    string randomCodeQuery = @"
                    SELECT code_generated_time 
                    FROM Users 
                    WHERE random_code = @code 
                    AND status2 = '1'";
                    SqlCommand randomCodeCmd = new SqlCommand(randomCodeQuery, conn);
                    randomCodeCmd.Parameters.AddWithValue("@code", cardNumber);

                    object result = randomCodeCmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                    {
                        if (DateTime.TryParse(result.ToString(), out DateTime codeTime))
                        {
                            TimeSpan timeDifference = DateTime.Now - codeTime;
                            if (timeDifference.TotalMinutes <= 1)
                            {
                                // Clear the code after successful use
                                string updateQuery = @"
                                UPDATE Users 
                                SET random_code = NULL, 
                                code_generated_time = NULL 
                                WHERE random_code = @code";
                                SqlCommand updateCmd = new SqlCommand(updateQuery, conn);
                                updateCmd.Parameters.AddWithValue("@code", cardNumber);
                                updateCmd.ExecuteNonQuery();

                                return (true, $"✅ Access Granted for code: {cardNumber}", ConsoleColor.Green);
                            }
                            else
                            {
                                // Clear expired code


                                return (false, $"❌ Code Expired: {cardNumber}", ConsoleColor.Red);
                            }
                        }
                        else
                        {
                            return (false, $"❌ Error: Invalid time format for code: {cardNumber}", ConsoleColor.Red);
                        }
                    }
                    else
                    {
                        return (false, $"❌ Invalid Code: {cardNumber}", ConsoleColor.Red);
                    }
                }
                catch (SqlException ex)
                {
                    return (false, $"❌ Database Connection Error for code: {cardNumber} - {ex.Message}", ConsoleColor.Red);
                }
                catch (Exception ex)
                {
                    return (false, $"❌ Unexpected Error for code: {cardNumber} - {ex.Message}", ConsoleColor.Red);
                }
            }
        }


        private void LogSwipe(string cardNumber, string status)
        {
            string logDir = @"C:\ES_LockScreen_Logs";
            string logPath = logDir + @"\log.txt";

            if (!System.IO.Directory.Exists(logDir))
                System.IO.Directory.CreateDirectory(logDir);

            string logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | Card: " + cardNumber + " | Status: " + status;

            try
            {
                System.IO.File.AppendAllText(logPath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Logging failed: " + ex.Message);
            }
        }

        private char VkCodeToChar(int vkCode)
        {
            if (vkCode >= 0x30 && vkCode <= 0x39)
                return (char)vkCode;
            if (vkCode >= 0x41 && vkCode <= 0x5A)
                return Char.ToLower((char)vkCode);
            return '?';
        }

        // Hide Taskbar
        [DllImport("user32.dll")]
        private static extern int FindWindow(string className, string windowText);
        [DllImport("user32.dll")]
        private static extern int ShowWindow(int hwnd, int command);
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        private void HideTaskbar()
        {
            int taskBarHandle = FindWindow("Shell_TrayWnd", "");
            ShowWindow(taskBarHandle, SW_HIDE);
        }

        private void ShowTaskbar()
        {
            int taskBarHandle = FindWindow("Shell_TrayWnd", "");
            ShowWindow(taskBarHandle, SW_SHOW);
        }

        private void DisableTaskManager()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
                key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                key.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not disable Task Manager: " + ex.Message);
            }
        }

        private void EnableTaskManager()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
                key.DeleteValue("DisableTaskMgr", false);
                key.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not enable Task Manager: " + ex.Message);
            }
        }
        // Native Windows API declarations
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

    }
}