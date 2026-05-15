namespace LiveFuelMap.DAL.Enums;

public enum UserRole
{
    Guest = 0,
    User = 1,
    Admin = 2
}

public enum SubscriptionFrequency
{
    Daily = 0,
    Weekly = 1
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
