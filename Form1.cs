using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ViolenceDistrictOverlay
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x80000;
        const int WS_EX_TRANSPARENT = 0x20;

        private bool isEditMode = true;
        private PictureBox pictureBox;
        private Label infoLabel;
        private System.Windows.Forms.Timer hotkeyTimer;
        private Point dragCursorPoint;
        private Point dragFormPoint;
        private ContextMenuStrip contextMenu;
        private NotifyIcon trayIcon; // Added for system tray

        private string mapsDirectory = string.Empty;
        private string[] mapFiles = new string[0];
        private int currentMapIndex = 0;

        public Form1()
        {
            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false; // Hidden from taskbar!
            this.StartPosition = FormStartPosition.Manual;
            this.Width = 320;
            this.Height = 350;

            this.TransparencyKey = Color.Magenta;

            Rectangle screenRect = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            this.Location = new Point(screenRect.Width - 350, 40);

            infoLabel = new Label();
            infoLabel.ForeColor = Color.White;
            infoLabel.BackColor = Color.FromArgb(230, 10, 10, 10);
            infoLabel.Dock = DockStyle.Bottom;
            infoLabel.Height = 45;
            infoLabel.TextAlign = ContentAlignment.MiddleCenter;
            infoLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            this.Controls.Add(infoLabel);

            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            this.Controls.Add(pictureBox);

            contextMenu = new ContextMenuStrip();
            var lockMenuItem = new ToolStripMenuItem("Toggle Lock / Unlock (F12)", null, (s, e) => ToggleEditMode());
            var exitMenuItem = new ToolStripMenuItem("Exit App", null, (s, e) => { trayIcon.Visible = false; Application.Exit(); });
            contextMenu.Items.Add(lockMenuItem);
            contextMenu.Items.Add(exitMenuItem);

            pictureBox.ContextMenuStrip = contextMenu;
            infoLabel.ContextMenuStrip = contextMenu;

            trayIcon = new NotifyIcon();
            trayIcon.Icon = new Icon(new MemoryStream(Properties.Resources.app_icon));
            trayIcon.Text = "Violence District Overlay";
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = contextMenu;

            SetupMapsDirectory();

            pictureBox.MouseDown += OnMouseDown;
            pictureBox.MouseMove += OnMouseMove;
            infoLabel.MouseDown += OnMouseDown;
            infoLabel.MouseMove += OnMouseMove;

            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            hotkeyTimer = new System.Windows.Forms.Timer();
            hotkeyTimer.Interval = 100;
            hotkeyTimer.Tick += HotkeyTimer_Tick;
            hotkeyTimer.Start();

            SetClickThrough(false);
        }

        private void Form1_Load(object sender, EventArgs e) { }

        private void HotkeyTimer_Tick(object sender, EventArgs e)
        {
            if ((GetAsyncKeyState(0x2D) & 0x8000) != 0)
            {
                ToggleEditMode();
                System.Threading.Thread.Sleep(300);
            }
        }

        private void ToggleEditMode()
        {
            SetClickThrough(isEditMode);
        }

        private void SetupMapsDirectory()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            mapsDirectory = Path.Combine(appDataPath, "ViolenceDistrictOverlay");
            if (!Directory.Exists(mapsDirectory))
            {
                Directory.CreateDirectory(mapsDirectory);
            }
            string[] existingMaps = Directory.GetFiles(mapsDirectory, "*.png");
            if (existingMaps.Length == 0)
            {
                try
                {
                    string[] mapNames = { "blood", "fire", "harbor", "mercy", "mma", "site68", "valde", "wood" };
                    foreach (string mapName in mapNames)
                    {
                        object resourceObj = Properties.Resources.ResourceManager.GetObject(mapName);
                        if (resourceObj is Bitmap bmp)
                        {
                            bmp.Save(Path.Combine(mapsDirectory, $"{mapName}.png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not extract default maps: " + ex.Message);
                }
            }

            LoadMapImages();
        }
        private void LoadMapImages()
        {
            if (!Directory.Exists(mapsDirectory)) { mapFiles = new string[0]; return; }
            mapFiles = Directory.GetFiles(mapsDirectory, "*.png");
            if (mapFiles.Length > 0)
            {
                if (currentMapIndex >= mapFiles.Length) currentMapIndex = 0;
                ShowCurrentMap();
            }
            else { pictureBox.BackColor = Color.FromArgb(50, 50, 50); }
        }

        private void ShowCurrentMap()
        {
            if (mapFiles.Length == 0) return;
            if (currentMapIndex >= mapFiles.Length) currentMapIndex = 0;
            if (currentMapIndex < 0) currentMapIndex = mapFiles.Length - 1;

            try
            {
                if (pictureBox.Image != null) pictureBox.Image.Dispose();
                pictureBox.Image = Image.FromFile(mapFiles[currentMapIndex]);
            }
            catch { }
            UpdateInfoText();
        }

        private void UpdateInfoText()
        {
            string mapName = (mapFiles.Length > 0) ? Path.GetFileName(mapFiles[currentMapIndex]) : "No maps";
            if (isEditMode)
            {
                infoLabel.Text = $"{mapName} ({currentMapIndex + 1}/{mapFiles.Length}) | F9/F10: Map | ±/↑↓: Resize | Insert: (Un)Lock";
            }
        }

        private void SetClickThrough(bool transparent)
        {
            Point currentLoc = this.Location;
            int currentWidth = this.Width;
            int currentHeight = this.Height;

            int style = GetWindowLong(this.Handle, GWL_EXSTYLE);
            if (transparent)
            {
                SetWindowLong(this.Handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);
                isEditMode = false;
                infoLabel.Visible = false;
                this.BackColor = Color.Magenta;
            }
            else
            {
                SetWindowLong(this.Handle, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
                isEditMode = true;
                infoLabel.Visible = true;
                this.BackColor = Color.FromArgb(30, 30, 30);
                LoadMapImages();
            }

            this.SetBounds(currentLoc.X, currentLoc.Y, currentWidth, currentHeight);
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (isEditMode)
            {
                if (e.KeyCode == Keys.F10) { currentMapIndex++; ShowCurrentMap(); }
                else if (e.KeyCode == Keys.F9) { currentMapIndex--; ShowCurrentMap(); }
                else if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Up || e.KeyCode == Keys.Right) { this.Width += 20; this.Height += 20; }
                else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.Down || e.KeyCode == Keys.Left) { this.Width = Math.Max(120, this.Width - 20); this.Height = Math.Max(120, this.Height - 20); }
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (isEditMode && e.Button == MouseButtons.Left)
            {
                dragCursorPoint = Cursor.Position;
            dragFormFormPoint:
                dragFormPoint = this.Location;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (isEditMode && e.Button == MouseButtons.Left)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(diff));
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            trayIcon.Visible = false; // Clean up tray icon on close
            base.OnFormClosed(e);
        }
    }
}