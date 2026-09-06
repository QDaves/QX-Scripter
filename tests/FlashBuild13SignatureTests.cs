using Qx.Headers.Flash;
using Xunit;

namespace QX.Tests;

public sealed class FlashBuild13SignatureTests
{
    static readonly (string Signature, string Name)[] SemanticBindings =
    [
        ("class3:out:_-62O._-lV", "Quit"),
        ("class3:out:_-91o._-jy", "RequestFurniInventory"),
        ("class3:in:_-oR._-L19", "FurniListInvalidate"),
        ("class3:out:_-K2D._-g1i", "GetPetInventory"),
        ("class3:out:_-L2y._-t1U", "GetBadges"),
        ("class3:in:_-o17._-O2U", "TradingConfirmation"),
        ("class3:in:_-o17._-P1W", "TradingCompleted"),
        ("class3:in:_-l1W._-wE", "PollError"),
        ("class3:out:com.sulake.habbo.communication.messages.outgoing.quest._-K1i", "GetQuests"),
        ("class3:out:com.sulake.habbo.communication.messages.outgoing.quest._-QO", "GetSeasonalQuestsOnly"),
        ("class3:in:_-OE._-H1R", "NewUserExperienceNotComplete"),
        ("class3:in:com.sulake.habbo.communication.messages.incoming.userdefinedroomevents._-62U", "WiredSaveSuccess"),
        ("class3:out:_-q1J._-I2C", "GetDailyTasks"),
        ("class3:out:_-8k._-O12", "GetHabbiconShopData"),
        ("class3:out:_-C20._-Rb", "GetAchievements"),
        ("class3:out:_-L2y._-Fr", "GetBadgePointLimits"),
        ("class3:out:_-g8._-l1z", "StartTyping"),
        ("class3:in:_-918._-M2k", "GiftReceiverNotFound")
    ];

    [Fact]
    public void FlashBuild13ResolvesEveryRequiredSemanticBinding()
    {
        SignatureDatabase database = SignatureDatabase.LoadDefault();
        foreach ((string signature, string name) in SemanticBindings)
        {
            Assert.True(database.TryResolve(signature, out string? resolved), signature);
            Assert.Equal(name, resolved);
        }
    }
}
