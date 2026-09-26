using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DiskLens.ViewModels;

namespace DiskLens.Views;

/// <summary>
/// Renders <see cref="AssistantViewModel.Entries"/> into one read-only FlowDocument, one Section per
/// entry. Streaming updates are coalesced so each dispatcher pass re-renders a changed entry once.
/// </summary>
public partial class AssistantPanel : UserControl
{
    private readonly Dictionary<ChatEntry, Section> _sections = [];
    private readonly HashSet<ChatEntry> _dirty = [];
    private readonly FlowDocument _document;
    private AssistantViewModel? _viewModel;
    private bool _renderQueued;

    public AssistantPanel()
    {
        InitializeComponent();

        _document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            PagePadding = new Thickness(0, 0, 8, 0),
            Foreground = (Brush)FindResource("TextBrush"),
        };
        Transcript.Document = _document;

        DataContextChanged += (_, _) => Attach(DataContext as AssistantViewModel);
    }

    private void Attach(AssistantViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }

        _viewModel = viewModel;
        Rebuild();

        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged += OnEntriesChanged;
            _viewModel.PropertyChanged += OnViewModelChanged;
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssistantViewModel.IsOpen) && _viewModel is { IsOpen: true })
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => InputBox.Focus());
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
        {
            Rebuild();
            return;
        }

        foreach (ChatEntry entry in e.NewItems)
            AddSection(entry);
        Transcript.ScrollToEnd();
    }

    private void Rebuild()
    {
        foreach (var entry in _sections.Keys)
            entry.PropertyChanged -= OnEntryChanged;

        _sections.Clear();
        _dirty.Clear();
        _document.Blocks.Clear();

        if (_viewModel is null)
            return;

        foreach (var entry in _viewModel.Entries)
            AddSection(entry);
    }

    private void AddSection(ChatEntry entry)
    {
        var section = new Section();
        _sections[entry] = section;
        _document.Blocks.Add(section);
        entry.PropertyChanged += OnEntryChanged;
        Render(entry, section);
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ChatEntry entry)
            return;

        _dirty.Add(entry);
        if (_renderQueued)
            return;

        _renderQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, RenderDirty);
    }

    private void RenderDirty()
    {
        _renderQueued = false;
        foreach (var entry in _dirty)
        {
            if (_sections.TryGetValue(entry, out var section))
                Render(entry, section);
        }

        _dirty.Clear();
        Transcript.ScrollToEnd();
    }

    private void Render(ChatEntry entry, Section section)
    {
        section.Blocks.Clear();

        switch (entry.Role)
        {
            case ChatRole.User:
                var question = new Paragraph(new Run(entry.Text))
                {
                    Background = (Brush)FindResource("ControlBrush"),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(40, 12, 0, 8),
                };
                section.Blocks.Add(question);
                break;

            case ChatRole.Error:
                section.Blocks.Add(new Paragraph(new Run(entry.Text))
                {
                    Foreground = (Brush)FindResource("WarningBrush"),
                    Margin = new Thickness(0, 8, 0, 8),
                });
                break;

            default:
                if (entry.Text.Length == 0 && entry.IsStreaming)
                {
                    section.Blocks.Add(new Paragraph(new Run("Thinking…"))
                    {
                        Foreground = (Brush)FindResource("TertiaryTextBrush"),
                        FontStyle = FontStyles.Italic,
                    });
                    break;
                }

                section.Blocks.AddRange(MarkdownRenderer.Render(entry.Text));
                break;
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.SendInputAsync();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _viewModel?.Stop();

    private void Close_Click(object sender, RoutedEventArgs e) => _viewModel?.Close();

    private async void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;

        e.Handled = true;
        if (_viewModel is not null)
            await _viewModel.SendInputAsync();
    }
}
