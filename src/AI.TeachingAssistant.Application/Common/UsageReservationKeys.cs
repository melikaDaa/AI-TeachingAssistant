namespace AI.TeachingAssistant.Application.Common;

public static class UsageReservationKeys
{
    public static string Image(string parentRequestKey, int index) => $"{parentRequestKey}:image:{index}";
}
