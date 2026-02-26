using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaloniaVirtualDataGrid.Controls;

namespace AvaloniaVirtualDataGrid.Columns;

public class VirtualDataGridTemplateColumn : VirtualDataGridColumn
{
    public IDataTemplate? CellTemplate { get; set; }
    public IDataTemplate? EditTemplate { get; set; }

    public override Control CreateCellContent(object? item)
    {
        if (CellTemplate != null && item != null)
        {
            var content = CellTemplate.Build(item);
            if (content != null)
                return content;
        }

        return new TextBlock { Text = string.Empty };
    }

    public override Control? CreateEditContent(object? item)
    {
        if (EditTemplate != null && item != null)
        {
            return EditTemplate.Build(item);
        }
        return null;
    }
}
