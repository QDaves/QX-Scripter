namespace Qx.Presentation.Collections;

public readonly record struct RowChanges(int Added, int Updated, int Removed)
{
    public bool ChangedMembership => Added > 0 || Removed > 0;
}
