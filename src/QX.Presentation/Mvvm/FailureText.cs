using Qx.Presentation.Services.Game;

namespace Qx.Presentation.Mvvm;

public static class FailureText
{
    public static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error switch
        {
            TimeoutException => "The hotel did not answer in time.",
            OperationCanceledException => "",
            GameUnavailableException unavailable => unavailable.Message,
            _ => error.Message
        };
    }
}
