using Flazzy.ABC;
using Xunit;

namespace QX.Tests;

public sealed class FlazzyRegressionTests
{
    [Fact]
    public void TypeNameWildcardSurvivesPoolResolutionAndAs3Formatting()
    {
        ABCFile abc = new();
        abc.Pool.Strings.Add(null);
        abc.Pool.Strings.Add("Vector");
        abc.Pool.Strings.Add("value");
        abc.Pool.Namespaces.Add(null);
        abc.Pool.Multinames.Add(null);

        ASMultiname vector = new(abc.Pool)
        {
            Kind = MultinameKind.QName,
            NameIndex = 1,
            NamespaceIndex = 0
        };
        abc.Pool.Multinames.Add(vector);

        ASMultiname vector_wildcard = new(abc.Pool)
        {
            Kind = MultinameKind.TypeName,
            QNameIndex = 1
        };
        vector_wildcard.TypeIndices.Add(0);
        abc.Pool.Multinames.Add(vector_wildcard);

        ASMultiname value = new(abc.Pool)
        {
            Kind = MultinameKind.QName,
            NameIndex = 2,
            NamespaceIndex = 0
        };
        abc.Pool.Multinames.Add(value);

        Assert.Equal([null], vector_wildcard.GetTypes());

        ASMethod method = new(abc)
        {
            ReturnTypeIndex = 2
        };
        method.Parameters.Add(new ASParameter(method)
        {
            TypeIndex = 2
        });

        Assert.Equal("function(param1:Vector.<*>):Vector.<*>", method.ToAS3());

        ASTrait trait = new(abc)
        {
            Kind = TraitKind.Slot,
            QNameIndex = 3,
            TypeIndex = 2
        };

        Assert.Equal("var value:Vector.<*>;", trait.ToAS3());
    }
}
