using System.Linq.Expressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaVirtualDataGrid.Controls;

namespace AvaloniaVirtualDataGrid.Columns;

public class VirtualDataGridTextColumn : VirtualDataGridColumn
{
    private Func<object?, object?>? _getter;
    private Action<object?, object?>? _setter;

    public VirtualDataGridTextColumn()
    {
    }

    public VirtualDataGridTextColumn(string header, Func<object?, object?> getter)
    {
        Header = header;
        _getter = getter;
    }

    public VirtualDataGridTextColumn(string header, Action<object?, object?> setter, Func<object?, object?> getter)
    {
        Header = header;
        _getter = getter;
        _setter = setter;
    }

    public static VirtualDataGridTextColumn Create<T>(string header, Expression<Func<T, object?>> propertyExpression)
    {
        var getter = propertyExpression.Compile();
        Action<object?, object?>? setter = null;

        // Handle both direct member access and conversion (when property type is value type cast to object)
        MemberExpression? memberExpr = propertyExpression.Body as MemberExpression;
        
        // If it's a conversion (e.g., string -> object), get the operand
        if (memberExpr == null && propertyExpression.Body is UnaryExpression unaryExpr && unaryExpr.NodeType == ExpressionType.Convert)
        {
            memberExpr = unaryExpr.Operand as MemberExpression;
        }

        if (memberExpr?.Member is System.Reflection.PropertyInfo propInfo && propInfo.CanWrite)
        {
            var param = Expression.Parameter(typeof(object), "value");
            Expression valueExpr = param;
            
            // Convert the value to the property type
            if (propInfo.PropertyType != typeof(object))
            {
                valueExpr = Expression.Convert(param, propInfo.PropertyType);
            }
            
            var call = Expression.Call(propertyExpression.Parameters[0], propInfo.GetSetMethod()!, valueExpr);
            var compiledSetter = Expression.Lambda<Action<T, object?>>(call, propertyExpression.Parameters[0], param).Compile();
            setter = (o, v) => { if (o is T t) compiledSetter(t, v); };
        }

        var column = new VirtualDataGridTextColumn(header, obj => obj is T t ? getter(t) : null);
        column._setter = setter;
        return column;
    }

    public override Control CreateCellContent(object? item)
    {
        var value = _getter?.Invoke(item);
        var text = value?.ToString() ?? string.Empty;

        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalContentAlignment,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 0)
        };
    }

    public object? GetRawValue(object? item)
    {
        return _getter?.Invoke(item);
    }

    public override Control? CreateEditContent(object? item)
    {
        if (_setter == null) return null;

        var value = _getter?.Invoke(item);
        var textBox = new TextBox
        {
            Text = value?.ToString() ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(2)
        };

        return textBox;
    }

    public override void CommitEdit(Control editControl, object? item)
    {
        if (editControl is TextBox textBox && _setter != null && item != null)
        {
            var currentValue = _getter?.Invoke(item);
            var newValue = textBox.Text;

            if (!Equals(currentValue?.ToString(), newValue))
            {
                var targetType = currentValue?.GetType() ?? typeof(string);
                object? convertedValue = newValue;
                
                if (targetType != typeof(string) && !string.IsNullOrEmpty(newValue))
                {
                    try
                    {
                        convertedValue = Convert.ChangeType(newValue, targetType);
                    }
                    catch
                    {
                        return;
                    }
                }
                
                _setter(item, convertedValue);
            }
        }
    }
}
