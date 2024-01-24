namespace Nerves.Shared.Configs.UsersConfigs.DataBaseOptions;

public class InsertUserOption
{
    public AlreadyExistsActions ActionWhenExists { get; set; } = AlreadyExistsActions.Skip;
}

[Flags]
public enum AlreadyExistsActions
{
    Skip = 1,
    Replace = 2,
    ThrowException = 4,
}

