using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameEditor.Controls;

/// <summary>
/// A dockable panel that can be dragged and docked to different sides or floated as a window.
/// </summary>
public class DockablePanel : Panel
{
    private bool _isDragging = false;
    private Point _dragStartPos;
    private Point _panelStartPos;
    private Form? _floatForm;
    private DockStyle _originalDock;
    private Control? _originalParent;
    private int _originalIndex;
    private bool _isFloating = false;
    private Label? _titleLabel;
    private Button? _dockButton;
    private Button? _closeButton;
    private Panel? _titleBar;
    private Control? _contentControl;
    private DockablePanel? _dragOverPanel;
    private DockStyle? _dragDockStyle;
    private Panel? _dragPreview;

    public string Title { get; set; } = "Panel";
    public bool ShowTitleBar { get; set; } = true;
    public bool CanFloat { get; set; } = true;
    public bool CanClose { get; set; } = false;

    public event EventHandler? PanelClosed;
    public event EventHandler? DockStateChanged;
    
    public bool IsFloating => _isFloating;
    

    public DockablePanel()
    {
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Color.FromArgb(240, 240, 240);
        
        CreateTitleBar();
    }

    private void CreateTitleBar()
    {
        if (!ShowTitleBar)
            return;

        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 25,
            BackColor = Color.FromArgb(220, 220, 220),
            BorderStyle = BorderStyle.FixedSingle
        };

        _titleLabel = new Label
        {
            Text = Title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0),
            ForeColor = Color.Black
        };

        if (CanClose)
        {
            _closeButton = new Button
            {
                Text = "×",
                Dock = DockStyle.Right,
                Width = 25,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Black,
                Font = new Font("Arial", 12, FontStyle.Bold)
            };
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.Click += (s, e) => OnPanelClosed();
            _titleBar.Controls.Add(_closeButton);
        }

        if (CanFloat)
        {
            _dockButton = new Button
            {
                Text = "⤢",
                Dock = DockStyle.Right,
                Width = 25,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Black
            };
            _dockButton.FlatAppearance.BorderSize = 0;
            _dockButton.Click += (s, e) => ToggleFloat();
            _titleBar.Controls.Add(_dockButton);
        }

        _titleBar.Controls.Add(_titleLabel);
        Controls.Add(_titleBar);

        // Make title bar draggable
        _titleBar.MouseDown += TitleBar_MouseDown;
        _titleBar.MouseMove += TitleBar_MouseMove;
        _titleBar.MouseUp += TitleBar_MouseUp;
        
        // Also make the title label draggable (but not buttons)
        _titleLabel.MouseDown += TitleBar_MouseDown;
        _titleLabel.MouseMove += TitleBar_MouseMove;
        _titleLabel.MouseUp += TitleBar_MouseUp;
    }

    public void SetContent(Control content)
    {
        if (_contentControl != null)
        {
            Controls.Remove(_contentControl);
        }

        _contentControl = content;
        _contentControl.Dock = DockStyle.Fill;
        Controls.Add(_contentControl);
        _contentControl.BringToFront();
    }

    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragStartPos = e.Location;
            
            if (_isFloating && _floatForm != null)
            {
                // When floating, track the form's screen position
                _panelStartPos = _floatForm.Location;
            }
            else
            {
                // When docked, convert to screen coordinates for dragging
                _panelStartPos = PointToScreen(Point.Empty);
            }
            
            // Capture mouse to continue receiving move events even outside the control
            if (_titleBar != null)
                _titleBar.Capture = true;
        }
    }

    private void TitleBar_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging && CanFloat)
        {
            var screenPos = _titleBar?.PointToScreen(e.Location) ?? Point.Empty;
            
            if (_isFloating && _floatForm != null)
            {
                // Move floating form
                var deltaX = screenPos.X - (_panelStartPos.X + _dragStartPos.X);
                var deltaY = screenPos.Y - (_panelStartPos.Y + _dragStartPos.Y);
                _floatForm.Location = new Point(
                    _panelStartPos.X + deltaX,
                    _panelStartPos.Y + deltaY
                );
                
                // Check for docking targets
                CheckDockTargets(screenPos);
            }
            else if (!_isFloating)
            {
                // When docked, check if we should float or find dock target
                var deltaX = screenPos.X - (_panelStartPos.X + _dragStartPos.X);
                var deltaY = screenPos.Y - (_panelStartPos.Y + _dragStartPos.Y);
                var dragDistance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                
                if (dragDistance > 10)
                {
                    // Check for docking targets first
                    if (!CheckDockTargets(screenPos))
                    {
                        // No dock target found, float the panel
                        FloatPanel();
                        if (_floatForm != null)
                        {
                            _panelStartPos = _floatForm.Location;
                            _dragStartPos = _floatForm.PointToClient(screenPos);
                        }
                    }
                }
            }
        }
    }
    
    private bool CheckDockTargets(Point screenPos)
    {
        // Find control under mouse
        var targetControl = FindControlAt(screenPos);
        var dockTarget = FindDockablePanel(targetControl);
        
        if (dockTarget != null && dockTarget != this)
        {
            _dragOverPanel = dockTarget;
            // Determine dock side based on position relative to target
            var targetScreenRect = dockTarget.RectangleToScreen(dockTarget.ClientRectangle);
            var relativeX = screenPos.X - targetScreenRect.X;
            var relativeY = screenPos.Y - targetScreenRect.Y;
            var centerX = targetScreenRect.Width / 2;
            var centerY = targetScreenRect.Height / 2;
            
            if (relativeX < centerX * 0.3)
                _dragDockStyle = DockStyle.Left;
            else if (relativeX > centerX * 1.7)
                _dragDockStyle = DockStyle.Right;
            else if (relativeY < centerY * 0.3)
                _dragDockStyle = DockStyle.Top;
            else if (relativeY > centerY * 1.7)
                _dragDockStyle = DockStyle.Bottom;
            else
                _dragDockStyle = DockStyle.Fill; // Center/tab
            
            ShowDockPreview();
            return true;
        }
        else
        {
            _dragOverPanel = null;
            _dragDockStyle = null;
            HideDockPreview();
            return false;
        }
    }
    
    private Control? FindControlAt(Point screenPos)
    {
        var form = FindForm();
        if (form == null) return null;
        
        var control = form.GetChildAtPoint(form.PointToClient(screenPos), GetChildAtPointSkip.Invisible);
        return control;
    }
    
    private DockablePanel? FindDockablePanel(Control? control)
    {
        while (control != null)
        {
            if (control is DockablePanel panel)
                return panel;
            control = control.Parent;
        }
        return null;
    }
    
    private void ShowDockPreview()
    {
        if (_dragOverPanel == null || _dragDockStyle == null) return;
        
        HideDockPreview();
        
        _dragPreview = new Panel
        {
            BackColor = Color.FromArgb(100, 0, 120, 215), // Semi-transparent blue
            BorderStyle = BorderStyle.FixedSingle
        };
        
        var targetRect = _dragOverPanel.RectangleToScreen(_dragOverPanel.ClientRectangle);
        var previewRect = new Rectangle();
        
        switch (_dragDockStyle.Value)
        {
            case DockStyle.Left:
                previewRect = new Rectangle(targetRect.X, targetRect.Y, targetRect.Width / 3, targetRect.Height);
                break;
            case DockStyle.Right:
                previewRect = new Rectangle(targetRect.Right - targetRect.Width / 3, targetRect.Y, targetRect.Width / 3, targetRect.Height);
                break;
            case DockStyle.Top:
                previewRect = new Rectangle(targetRect.X, targetRect.Y, targetRect.Width, targetRect.Height / 3);
                break;
            case DockStyle.Bottom:
                previewRect = new Rectangle(targetRect.X, targetRect.Bottom - targetRect.Height / 3, targetRect.Width, targetRect.Height / 3);
                break;
            case DockStyle.Fill:
                previewRect = targetRect;
                break;
        }
        
        var form = FindForm();
        if (form != null)
        {
            _dragPreview.Location = form.PointToClient(previewRect.Location);
            _dragPreview.Size = previewRect.Size;
            form.Controls.Add(_dragPreview);
            _dragPreview.BringToFront();
        }
    }
    
    private void HideDockPreview()
    {
        if (_dragPreview != null)
        {
            var parent = _dragPreview.Parent;
            parent?.Controls.Remove(_dragPreview);
            _dragPreview.Dispose();
            _dragPreview = null;
        }
    }

    private void TitleBar_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            if (_titleBar != null)
                _titleBar.Capture = false;
            
            // Handle docking if we were dragging over a target
            if (_dragOverPanel != null && _dragDockStyle != null)
            {
                DockToPanel(_dragOverPanel, _dragDockStyle.Value);
            }
            
            HideDockPreview();
            _dragOverPanel = null;
            _dragDockStyle = null;
        }
    }
    
    private void DockToPanel(DockablePanel target, DockStyle dockStyle)
    {
        if (target == this) return;
        
        // Remove from current parent
        var currentParent = Parent;
        currentParent?.Controls.Remove(this);
        
        if (_isFloating && _floatForm != null)
        {
            _floatForm.Controls.Remove(this);
            _floatForm.Close();
            _floatForm.Dispose();
            _floatForm = null;
            _isFloating = false;
        }
        
        // Store original state for undocking
        _originalDock = Dock;
        _originalParent = currentParent;
        
        if (dockStyle == DockStyle.Fill)
        {
            // Tab docking - replace target's content
            var targetContent = target._contentControl;
            if (targetContent != null)
            {
                target.Controls.Remove(targetContent);
            }
            target.SetContent(this);
            Dock = DockStyle.Fill;
        }
        else
        {
            // Side docking - create split container
            var targetParent = target.Parent;
            if (targetParent != null)
            {
                var splitContainer = new SplitContainer
                {
                    Dock = target.Dock,
                    Orientation = (dockStyle == DockStyle.Left || dockStyle == DockStyle.Right) 
                        ? Orientation.Horizontal 
                        : Orientation.Vertical
                };
                
                // Replace target with split container
                var targetIndex = targetParent.Controls.IndexOf(target);
                targetParent.Controls.Remove(target);
                targetParent.Controls.Add(splitContainer);
                if (targetIndex >= 0)
                    targetParent.Controls.SetChildIndex(splitContainer, targetIndex);
                
                // Add panels to split container
                if (dockStyle == DockStyle.Left || dockStyle == DockStyle.Top)
                {
                    splitContainer.Panel1.Controls.Add(this);
                    splitContainer.Panel2.Controls.Add(target);
                    Dock = DockStyle.Fill;
                    target.Dock = DockStyle.Fill;
                }
                else
                {
                    splitContainer.Panel1.Controls.Add(target);
                    splitContainer.Panel2.Controls.Add(this);
                    Dock = DockStyle.Fill;
                    target.Dock = DockStyle.Fill;
                }
                
                // Set splitter distance
                splitContainer.SplitterDistance = splitContainer.Width / 2;
            }
        }
        
        if (_dockButton != null)
            _dockButton.Text = "⤢";
        
        DockStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleFloat()
    {
        if (_isFloating)
        {
            DockPanelBack();
        }
        else
        {
            FloatPanel();
        }
    }

    private void FloatPanel()
    {
        if (!CanFloat)
            return;

        // Store original state
        _originalDock = Dock;
        _originalParent = Parent;
        _originalIndex = Parent?.Controls.IndexOf(this) ?? -1;

        // Calculate screen position - use current mouse position if dragging, otherwise use panel position
        Point screenLocation;
        if (_isDragging && _titleBar != null)
        {
            var mouseScreenPos = _titleBar.PointToScreen(_dragStartPos);
            screenLocation = new Point(
                mouseScreenPos.X - _dragStartPos.X,
                mouseScreenPos.Y - _dragStartPos.Y
            );
        }
        else
        {
            screenLocation = PointToScreen(Point.Empty);
        }

        // Ensure floating window doesn't go off-screen or overlap with main form
        var mainForm = FindForm();
        if (mainForm != null)
        {
            var mainFormScreenBounds = mainForm.RectangleToScreen(mainForm.ClientRectangle);
            var screenBounds = Screen.FromControl(mainForm).WorkingArea;
            
            // Offset from main form to avoid overlap
            screenLocation = new Point(
                Math.Max(screenBounds.Left + 20, Math.Min(screenLocation.X, screenBounds.Right - 300)),
                Math.Max(screenBounds.Top + 20, Math.Min(screenLocation.Y, screenBounds.Bottom - 200))
            );
        }

        // Create floating form
        _floatForm = new Form
        {
            Text = Title,
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
            StartPosition = FormStartPosition.Manual,
            Size = new Size(Math.Max(250, Width + 16), Math.Max(200, Height + 39)), // Account for borders, ensure minimum size
            Location = screenLocation,
            ShowInTaskbar = false,
            MinimumSize = new Size(250, 200) // Ensure floating windows have minimum size
        };

        // Remove from parent
        Parent?.Controls.Remove(this);

        // Add to floating form
        Dock = DockStyle.Fill;
        _floatForm.Controls.Add(this);

        _isFloating = true;
        _floatForm.Show();

        if (_dockButton != null)
            _dockButton.Text = "⤓";

        DockStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Docks the panel back to its original position if it's floating.
    /// </summary>
    public void DockPanelBack()
    {
        if (!_isFloating || _floatForm == null || _originalParent == null)
            return;

        // Remove from floating form
        _floatForm.Controls.Remove(this);
        _floatForm.Close();
        _floatForm.Dispose();
        _floatForm = null;

        // Restore to original parent
        Dock = _originalDock;
        _originalParent.Controls.Add(this);
        if (_originalIndex >= 0 && _originalIndex < _originalParent.Controls.Count)
        {
            _originalParent.Controls.SetChildIndex(this, _originalIndex);
        }

        _isFloating = false;

        if (_dockButton != null)
            _dockButton.Text = "⤢";

        DockStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPanelClosed()
    {
        if (_isFloating && _floatForm != null)
        {
            _floatForm.Close();
            _floatForm.Dispose();
            _floatForm = null;
        }
        else
        {
            Parent?.Controls.Remove(this);
        }

        PanelClosed?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_floatForm != null)
            {
                _floatForm.Close();
                _floatForm.Dispose();
                _floatForm = null;
            }
        }
        base.Dispose(disposing);
    }
}

