using System.Windows.Input;

namespace Qx.Presentation.Mvvm;

public sealed record OperationProgress(string Text, double? Fraction = null, ICommand? Cancel = null);
