using Qx.Presentation.Visuals;

namespace Qx.Presentation.Navigation;

public sealed record PageDescriptor(PageKey Key, string Title, string RailTip, IconKind Icon, PageGroup Group, int Order, string CommandId)
{
    public bool InRail => Order > 0;
}
