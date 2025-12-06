using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Forms;

namespace GameEditor.Services;

/// <summary>
/// Singleton service that manages a single shared game process for all editor windows.
/// All game view windows share the same game instance and controls.
/// </summary>
public class GameHostService
{
    private static GameHostService? _instance;
    private static readonly object _lock = new object();
    
    private Process? _gameProcess;
    private IntPtr _gameWindowHandle = IntPtr.Zero;
    private bool _isGameRunning = false;
    private System.Windows.Forms.Timer? _findWindowTimer;
    private int _embedAttempts = 0;
    private const int MAX_EMBED_ATTEMPTS = 100;
    
    // Events for notifying when game starts/stops
    public event EventHandler? GameStarted;
    public event EventHandler? GameStopped;
    
    // Windows API functions
    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    private const int GWL_STYLE = -16;
    private const uint WS_CHILD = 0x40000000;
    private const int SW_SHOW = 5;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    
    public static GameHostService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new GameHostService();
                    }
                }
            }
            return _instance;
        }
    }
    
    private GameHostService()
    {
    }
    
    public bool IsGameRunning => _isGameRunning;
    public IntPtr GameWindowHandle => _gameWindowHandle;
    
    public void LaunchGame(IntPtr parentHandle, Action<IntPtr>? onWindowFound = null, bool embedWindow = false)
    {
        if (_isGameRunning)
        {
            // Game already running
            if (_gameWindowHandle != IntPtr.Zero)
            {
                if (embedWindow)
                {
                    // Embed it in the new parent
                    EmbedWindow(_gameWindowHandle, parentHandle);
                }
                else
                {
                    // Make sure it's not embedded (floating window)
                    UnembedWindow(_gameWindowHandle);
                }
                onWindowFound?.Invoke(_gameWindowHandle);
            }
            return;
        }
        
        try
        {
            // Find the game executable
            string? gameExePath = FindGameExecutable();
            
            if (gameExePath == null || !File.Exists(gameExePath))
            {
                ShowGameNotFoundError();
                return;
            }
            
            // Start the game process
            _gameProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = gameExePath,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(gameExePath)
                },
                EnableRaisingEvents = true
            };
            
            _gameProcess.Exited += (s, e) =>
            {
                StopGame();
            };
            
            var started = _gameProcess.Start();
            if (!started)
            {
                MessageBox.Show("Failed to start the game process.", "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            _isGameRunning = true;
            _embedAttempts = 0;
            
            GameStarted?.Invoke(this, EventArgs.Empty);
            
            // Wait for process to initialize
            System.Threading.Thread.Sleep(1000);
            
            // Start timer to find the game window
            _findWindowTimer = new System.Windows.Forms.Timer
            {
                Interval = 100
            };
            _findWindowTimer.Tick += (s, e) => FindGameWindow(parentHandle, onWindowFound, embedWindow);
            _findWindowTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error launching game: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _isGameRunning = false;
        }
    }
    
    private void FindGameWindow(IntPtr parentHandle, Action<IntPtr>? onWindowFound, bool embedWindow)
    {
        if (_gameProcess == null || _gameProcess.HasExited)
        {
            StopGame();
            return;
        }
        
        _embedAttempts++;
        
        if (_gameWindowHandle == IntPtr.Zero)
        {
            _gameWindowHandle = FindGameWindow(_gameProcess.Id);
            if (_gameWindowHandle != IntPtr.Zero)
            {
                if (embedWindow)
                {
                    EmbedWindow(_gameWindowHandle, parentHandle);
                }
                else
                {
                    // Ensure window is floating and movable
                    UnembedWindow(_gameWindowHandle);
                }
                _findWindowTimer?.Stop();
                _embedAttempts = 0;
                onWindowFound?.Invoke(_gameWindowHandle);
            }
            else if (_embedAttempts >= MAX_EMBED_ATTEMPTS)
            {
                _findWindowTimer?.Stop();
                if (embedWindow)
                {
                    MessageBox.Show(
                        "Could not find the game window to embed. The game may be running in a separate window.",
                        "Window Embedding",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }
    }
    
    public void StopGame()
    {
        if (_findWindowTimer != null)
        {
            _findWindowTimer.Stop();
            _findWindowTimer.Dispose();
            _findWindowTimer = null;
        }
        
        if (_gameWindowHandle != IntPtr.Zero)
        {
            try
            {
                SetParent(_gameWindowHandle, IntPtr.Zero);
            }
            catch
            {
                // Ignore errors
            }
            _gameWindowHandle = IntPtr.Zero;
        }
        
        if (_gameProcess != null && !_gameProcess.HasExited)
        {
            try
            {
                _gameProcess.Kill();
            }
            catch
            {
                // Ignore errors
            }
            _gameProcess.Dispose();
            _gameProcess = null;
        }
        
        _isGameRunning = false;
        _embedAttempts = 0;
        
        GameStopped?.Invoke(this, EventArgs.Empty);
    }
    
    public void EmbedWindow(IntPtr gameHandle, IntPtr parentHandle)
    {
        try
        {
            int style = GetWindowLong(gameHandle, GWL_STYLE);
            style &= ~(int)0x00C00000; // Remove WS_CAPTION
            style &= ~(int)0x00040000; // Remove WS_THICKFRAME
            style &= ~(int)0x00020000; // Remove WS_MINIMIZEBOX
            style &= ~(int)0x00010000; // Remove WS_MAXIMIZEBOX
            style |= (int)WS_CHILD;
            SetWindowLong(gameHandle, GWL_STYLE, (uint)style);
            
            SetParent(gameHandle, parentHandle);
            ShowWindow(gameHandle, SW_SHOW);
        }
        catch
        {
            // Ignore embedding errors
        }
    }
    
    public void UnembedWindow(IntPtr gameHandle)
    {
        try
        {
            // Remove from parent (make it a top-level window)
            SetParent(gameHandle, IntPtr.Zero);
            
            // Restore window style to make it a normal movable window
            int style = GetWindowLong(gameHandle, GWL_STYLE);
            style &= ~(int)WS_CHILD; // Remove WS_CHILD
            style |= 0x00C00000; // Add WS_CAPTION (title bar)
            style |= 0x00040000; // Add WS_THICKFRAME (resizable border)
            style |= 0x00020000; // Add WS_MINIMIZEBOX
            style |= 0x00010000; // Add WS_MAXIMIZEBOX
            SetWindowLong(gameHandle, GWL_STYLE, (uint)style);
            
            ShowWindow(gameHandle, SW_SHOW);
        }
        catch
        {
            // Ignore errors
        }
    }
    
    public void ResizeGameWindow(IntPtr gameHandle, IntPtr parentHandle, int width, int height)
    {
        if (gameHandle == IntPtr.Zero)
            return;
        
        try
        {
            // Center the game window within the parent
            SetWindowPos(gameHandle, IntPtr.Zero, 0, 0, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        catch
        {
            // Ignore resize errors
        }
    }
    
    private IntPtr FindGameWindow(int processId)
    {
        IntPtr foundWindow = IntPtr.Zero;
        
        EnumWindowsProc enumProc = (hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;
            
            GetWindowThreadProcessId(hWnd, out uint processIdFromWindow);
            
            if (processIdFromWindow == processId)
            {
                int style = GetWindowLong(hWnd, GWL_STYLE);
                bool isTopLevel = (style & 0x40000000) == 0; // Not WS_CHILD
                
                if (isTopLevel)
                {
                    foundWindow = hWnd;
                    return false;
                }
            }
            return true;
        };
        
        EnumWindows(enumProc, IntPtr.Zero);
        return foundWindow;
    }
    
    private string? FindGameExecutable()
    {
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "GameClient", "bin", "Debug", "net8.0", "GameClient.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "GameClient", "bin", "Debug", "net8.0", "GameClient.exe"),
            Path.Combine(Application.StartupPath, "..", "GameClient", "bin", "Debug", "net8.0", "GameClient.exe"),
            Path.Combine(Application.StartupPath, "..", "..", "GameClient", "bin", "Debug", "net8.0", "GameClient.exe"),
            Path.Combine(Application.StartupPath, "..", "..", "..", "GameClient", "bin", "Debug", "net8.0", "GameClient.exe"),
            Path.Combine(Application.StartupPath, "..", "..", "..", "..", "GameClient", "bin", "Debug", "net8.0", "GameClient.exe"),
        };
        
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        
        return null;
    }
    
    private void ShowGameNotFoundError()
    {
        var errorMsg = "Game executable not found.\n\nPlease build the GameClient project first.";
        MessageBox.Show(errorMsg, "Game Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

