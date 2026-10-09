using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using SysLens.Ai;

namespace SysLens.ViewModels;

public enum ChatRole
{
    User,
    Assistant,
    Error,
}

public sealed class ChatEntry(ChatRole role, string text) : ObservableObject
{
    private string _text = text;
    private bool _isStreaming;

    public ChatRole Role { get; } = role;

    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        set => Set(ref _isStreaming, value);
    }
}

/// <summary>A conversation with the AI about one item: a folder or file, or a USB device.</summary>
public sealed class AssistantViewModel : ObservableObject
{
    private ChatClient? _client;
    private List<ChatMessage> _history = [];
    private CancellationTokenSource? _cts;
    private AssistantTopic? _topic;
    private string _input = "";
    private bool _isOpen;
    private bool _isBusy;

    public ObservableCollection<ChatEntry> Entries { get; } = [];

    public AssistantTopic? Topic
    {
        get => _topic;
        private set => Set(ref _topic, value);
    }

    public string Input
    {
        get => _input;
        set => Set(ref _input, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => Set(ref _isOpen, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsBusy;

    public Task StartAsync(AssistantTopic topic)
    {
        Stop();
        // A new list, not Clear(): a reply still finishing for the previous item appends to its own history.
        _history = [new ChatMessage("system", topic.SystemPrompt)];
        Entries.Clear();
        Topic = topic;
        Input = "";
        IsOpen = true;
        return SendAsync(topic.Question, topic.QuestionDisplay);
    }

    public Task SendInputAsync()
    {
        var text = Input.Trim();
        if (text.Length == 0 || IsBusy || Topic is null)
            return Task.CompletedTask;

        Input = "";
        return SendAsync(text, text);
    }

    public void Stop() => _cts?.Cancel();

    public void Close()
    {
        Stop();
        IsOpen = false;
    }

    private async Task SendAsync(string content, string display)
    {
        if (GetClient() is not { } client)
            return;

        var history = _history;
        history.Add(new ChatMessage("user", content));
        Entries.Add(new ChatEntry(ChatRole.User, display));

        var reply = new ChatEntry(ChatRole.Assistant, "") { IsStreaming = true };
        Entries.Add(reply);

        using var cts = new CancellationTokenSource();
        _cts = cts;
        IsBusy = true;

        var answer = new StringBuilder();
        try
        {
            await foreach (var chunk in client.StreamAsync(history, cts.Token))
            {
                answer.Append(chunk);
                reply.Text = answer.ToString();
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            reply.Text = answer + "\n\n*(stopped)*";
        }
        catch (Exception ex) when (ex is HttpRequestException or ChatException or JsonException or IOException
                                       or TaskCanceledException)
        {
            if (ReferenceEquals(history, _history))
            {
                if (answer.Length == 0)
                    Entries.Remove(reply);
                Entries.Add(new ChatEntry(ChatRole.Error, ex.Message));
            }
        }
        finally
        {
            reply.IsStreaming = false;
            if (answer.Length > 0)
                history.Add(new ChatMessage("assistant", answer.ToString()));
            else
                history.RemoveAt(history.Count - 1); // Unanswered question: drop it so a retry starts clean.

            if (_cts == cts)
            {
                _cts = null;
                IsBusy = false;
            }
        }
    }

    private ChatClient? GetClient()
    {
        if (_client is not null)
            return _client;

        try
        {
            return _client = new ChatClient(AiConfig.Load());
        }
        catch (AiConfigException ex)
        {
            Entries.Add(new ChatEntry(ChatRole.Error, ex.Message));
            return null;
        }
    }
}
