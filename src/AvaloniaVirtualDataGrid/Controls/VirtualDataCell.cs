using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataCell : ContentControl
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<VirtualDataCell, bool>(nameof(IsSelected));

    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<VirtualDataCell, bool>(nameof(IsEditing));

    public static readonly StyledProperty<int> ColumnIndexProperty =
        AvaloniaProperty.Register<VirtualDataCell, int>(nameof(ColumnIndex), -1);

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    public int ColumnIndex
    {
        get => GetValue(ColumnIndexProperty);
        set => SetValue(ColumnIndexProperty, value);
    }

    private VirtualDataGridColumn? _column;
    public VirtualDataGridColumn? Column
    {
        get => _column;
        set
        {
            _column = value;
            UpdateCell();
        }
    }

    private Control? _editContent;
    private bool _isInternalEdit;

    public VirtualDataCell()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEditingProperty)
        {
            UpdateEditingState();
        }
        else if (change.Property == IsSelectedProperty)
        {
            UpdateVisualState();
        }
        else if (change.Property == DataContextProperty)
        {
            UpdateCell();
        }
    }

    internal void UpdateCell()
    {
        if (Column == null || DataContext == null || IsEditing)
            return;

        _isInternalEdit = true;
        Content = Column.CreateCellContent(DataContext);
        _isInternalEdit = false;
    }

    private void UpdateEditingState()
    {
        if (_isInternalEdit) return;

        if (IsEditing && Column != null && DataContext != null)
        {
            _editContent = Column.CreateEditContent(DataContext);
            if (_editContent != null)
            {
                _isInternalEdit = true;
                Content = _editContent;
                _isInternalEdit = false;
                
                if (_editContent is TextBox textBox)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                    textBox.LostFocus += OnEditLostFocus;
                    textBox.KeyDown += OnEditKeyDown;
                }
            }
        }
        else
        {
            if (_editContent is TextBox textBox)
            {
                textBox.LostFocus -= OnEditLostFocus;
                textBox.KeyDown -= OnEditKeyDown;
            }
            _editContent = null;
            
            if (Column != null && DataContext != null)
            {
                _isInternalEdit = true;
                Content = Column.CreateCellContent(DataContext);
                _isInternalEdit = false;
            }
        }
    }

    private void OnEditLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitEdit();
    }

    private void OnEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    public void CommitEdit()
    {
        if (Column != null && DataContext != null && _editContent != null)
        {
            Column.CommitEdit(_editContent, DataContext);
            _editContent = null;
        }
        IsEditing = false;
    }

    public void CancelEdit()
    {
        _editContent = null;
        IsEditing = false;
    }

    private void UpdateVisualState()
    {
        if (IsSelected)
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
            BorderThickness = new Thickness(0, 0, 1, 0);
        }
        else
        {
            BorderBrush = null;
            BorderThickness = new Thickness(0);
        }
    }
}
