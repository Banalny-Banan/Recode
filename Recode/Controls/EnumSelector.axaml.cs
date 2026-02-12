using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Recode.Controls;

public enum EnumSelectorMode
{
    List,
    Dropdown,
}

public partial class EnumSelector : UserControl
{
    public static readonly StyledProperty<object?> SelectedValueProperty =
        AvaloniaProperty.Register<EnumSelector, object?>(
            nameof(SelectedValue),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<EnumSelectorMode> ModeProperty =
        AvaloniaProperty.Register<EnumSelector, EnumSelectorMode>(nameof(Mode));

    readonly List<EnumItem> _items = [];
    bool _syncing;

    public EnumSelector()
    {
        InitializeComponent();
        ItemsListBox.SelectionChanged += OnSelectionChanged;
        ItemsComboBox.SelectionChanged += OnSelectionChanged;

        if (Design.IsDesignMode)
        {
            var designItems = new[] { new EnumItem(0, "Item 1"), new EnumItem(1, "Item 2"), new EnumItem(2, "Item 3"), new EnumItem(3, "Item 4") };
            ItemsListBox.ItemsSource = designItems;
            ItemsComboBox.ItemsSource = designItems;
        }
    }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public EnumSelectorMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ModeProperty)
        {
            bool isList = (EnumSelectorMode)change.NewValue! == EnumSelectorMode.List;
            ItemsListBox.IsVisible = isList;
            ItemsComboBox.IsVisible = !isList;
        }
        else if (change.Property == SelectedValueProperty && !_syncing)
        {
            object? newValue = change.NewValue;
            if (newValue == null) return;

            if (_items.Count == 0)
                Populate(newValue.GetType());

            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Value.Equals(newValue))
                {
                    _syncing = true;
                    ItemsListBox.SelectedIndex = i;
                    ItemsComboBox.SelectedIndex = i;
                    _syncing = false;
                    break;
                }
            }
        }
    }

    void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;

        if (sender is SelectingItemsControl { SelectedIndex: >= 0 } control
            && control.SelectedIndex < _items.Count)
        {
            _syncing = true;
            SelectedValue = _items[control.SelectedIndex].Value;
            _syncing = false;
        }
    }

    void Populate(Type enumType)
    {
        foreach (object value in Enum.GetValues(enumType))
        {
            var name = value.ToString()!;
            FieldInfo? field = enumType.GetField(name);
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            _items.Add(new EnumItem(value, attr?.Description ?? name));
        }

        ItemsListBox.ItemsSource = _items;
        ItemsComboBox.ItemsSource = _items;
    }

    record EnumItem(object Value, string DisplayName);
}