namespace Qx.Model;

public enum AvatarType
{
    User = 1,
    Pet = 2,
    PublicBot = 3,
    PrivateBot = 4
}

public enum Gender
{
    None = -1,
    Female = 0,
    Male = 1,
    Unisex = 2
}

public static class Genders
{
    public static Gender Parse(string value) => value.ToLowerInvariant() switch
    {
        "m" or "male" => Gender.Male,
        "f" or "female" => Gender.Female,
        "u" or "unisex" => Gender.Unisex,
        _ => Gender.None
    };

    public static string ToClientString(this Gender gender) => gender switch
    {
        Gender.Female => "F",
        Gender.Male => "M",
        _ => "U"
    };
}
