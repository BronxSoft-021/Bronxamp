using System.Windows;
using System.Windows.Controls;
using Bronxamp.Vm;

namespace Bronxamp.Prikaz;

public partial class FolderiPrikaz : UserControl
{
    public FolderiPrikaz() => InitializeComponent();

    private void NaIzbor(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is FolderiStrana strana && e.NewValue is FolderCvor cvor) _ = strana.Otvori(cvor);
    }
}
