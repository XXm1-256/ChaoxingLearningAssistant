using ChaoxingLearningAssistant.Models;
using ChaoxingLearningAssistant.Services;

namespace ChaoxingLearningAssistant.StateMachine;

public sealed class RunStateMachine
{
    private readonly FileLogger _logger;

    public AppRunState Current { get; private set; } = AppRunState.Idle;

    public event EventHandler<AppRunState>? StateChanged;

    public RunStateMachine(FileLogger logger)
    {
        _logger = logger;
    }

    public void Transition(AppRunState next, string reason)
    {
        if (Current == next)
            return;

        var previous = Current;
        Current = next;
        _logger.Info("STATE", $"{previous} -> {next}；{reason}");
        StateChanged?.Invoke(this, next);
    }
}
