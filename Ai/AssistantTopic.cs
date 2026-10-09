namespace SysLens.Ai;

/// <summary>What a new AI conversation is about and how it opens.</summary>
/// <param name="Subject">Shown under the assistant panel title.</param>
/// <param name="SystemPrompt">Role and rules for the whole conversation.</param>
/// <param name="Question">First message sent to the model, carrying the item's details.</param>
/// <param name="QuestionDisplay">Short form of <paramref name="Question"/> shown in the transcript.</param>
public sealed record AssistantTopic(string Subject, string SystemPrompt, string Question, string QuestionDisplay);
