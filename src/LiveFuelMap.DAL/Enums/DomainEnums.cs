namespace LiveFuelMap.DAL.Enums;

public enum UserRole
{
    Guest = 0,
    User = 1,
    Admin = 2
}

public enum SubscriptionFrequency
{
    Immediate = 0,
    Daily = 1,
    Weekly = 2
}

public enum DataSourceType
{
    Html = 0,
    Json = 1
}

public enum ParserRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2
}
