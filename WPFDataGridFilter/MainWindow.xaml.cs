using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFDataGridFilter.ViewModels;

namespace WPFDataGridFilter;

/// <summary>
/// メインウィンドウのコードビハインド。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void LogGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is not DataGridRow row)
        {
            return;
        }

        if (row.Item is null)
        {
            return;
        }

        var properties = TypeDescriptor.GetProperties(row.Item);
        if (properties.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        foreach (PropertyDescriptor descriptor in properties)
        {
            if (!descriptor.IsBrowsable)
            {
                continue;
            }

            var value = descriptor.GetValue(row.Item) ?? string.Empty;
            builder.AppendLine($"{descriptor.DisplayName}: {value}");
        }

        if (builder.Length == 0)
        {
            return;
        }

        MessageBox.Show(this, builder.ToString(), "行のプロパティ", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}