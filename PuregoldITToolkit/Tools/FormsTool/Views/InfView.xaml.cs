using PuregoldITToolkit.Tools.FormsTool.ViewModels;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PuregoldITToolkit.Tools.FormsTool.Views
{
    public partial class InfView : UserControl
    {
        private InfViewModel _subscribed;
        private readonly List<DataGridColumn> _customColumns = new List<DataGridColumn>();
        public InfView()
        {
            InitializeComponent();
            Loaded += (s, e) => Connect();
            DataContextChanged += (s, e) => { if (IsLoaded) Connect(); };
            Unloaded += (s, e) => Disconnect();
        }
        private void Disconnect()
        {
            if (_subscribed != null) _subscribed.CustomColumns.CollectionChanged -= OnColumnsChanged;
            _subscribed = null;
        }
        private void Connect()
        {
            Disconnect();
            _subscribed = DataContext as InfViewModel;
            if (_subscribed != null) _subscribed.CustomColumns.CollectionChanged += OnColumnsChanged;
            RebuildColumns();
        }
        private void OnColumnsChanged(object sender, NotifyCollectionChangedEventArgs e) { RebuildColumns(); }
        private void RebuildColumns()
        {
            ItemsTable.CommitEdit(DataGridEditingUnit.Cell, true);
            ItemsTable.CommitEdit(DataGridEditingUnit.Row, true);
            foreach (var column in _customColumns) ItemsTable.Columns.Remove(column);
            _customColumns.Clear();
            if (_subscribed == null) return;
            BindingOperations.SetBinding(GeneratedSkuColumn, DataGridColumn.VisibilityProperty,
                new Binding(nameof(InfViewModel.IsType4Apar)) { Source = _subscribed, Converter = new BooleanToVisibilityConverter() });
            for (int i = 0; i < _subscribed.CustomColumns.Count; i++)
            {
                var column = new DataGridTextColumn
                {
                    Header = _subscribed.CustomColumns[i], Width = new DataGridLength(150),
                    Binding = new Binding($"CustomFields[{i}].Value") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
                };
                ItemsTable.Columns.Insert(ItemsTable.Columns.Count - 1, column);
                _customColumns.Add(column);
            }
        }
    }
}
