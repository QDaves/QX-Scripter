using System.Reflection;
using Qx.Game.Protocol;
using Qx.Protocol;

namespace Qx.Hosting;

internal static class MessageMembers
{
    public static IReadOnlyDictionary<MessageKey, MessageRegistryMembers> Declared(MessageContractCatalog contracts)
    {
        var keys = new Dictionary<MessageKey, string>();
        AddDeclarations(typeof(MessageKeys), keys);
        var contract_members = new Dictionary<IMessageContract, string>();
        AddDeclarations(typeof(MessageContracts), contract_members);

        var members = new Dictionary<MessageKey, MessageRegistryMembers>(keys.Count);
        foreach ((MessageKey key, string key_member) in keys)
        {
            members.Add(key, contracts.TryGet(key, out IMessageContract contract) &&
                contract_members.TryGetValue(contract, out string? contract_member)
                    ? new MessageRegistryMembers(key_member, contract.MessageType, contract_member)
                    : new MessageRegistryMembers(key_member, null, null));
        }
        return members;
    }

    private static void AddDeclarations<T>(Type type, Dictionary<T, string> declarations) where T : notnull
    {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is T value)
                declarations.Add(value, $"{type.FullName!.Replace('+', '.')}.{field.Name}");
        }

        foreach (Type nested in type.GetNestedTypes())
            AddDeclarations(nested, declarations);
    }
}
