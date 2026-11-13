namespace StardewMCP.Agent;

public enum ShopAvailability
{
    Open,
    BeforeOpen,
    ClosingSoon,
    AfterClose,
    ClosedDay,
    Festival
}

public static class ShopRules
{
    public static ShopAvailability Evaluate(int now, int open, int close, bool closedDay, bool festival, int closingBuffer = 30)
    {
        if (closedDay) return ShopAvailability.ClosedDay;
        if (festival) return ShopAvailability.Festival;
        if (now < open) return ShopAvailability.BeforeOpen;
        if (now >= close) return ShopAvailability.AfterClose;
        int nowMinutes = now / 100 * 60 + now % 100;
        int closeMinutes = close / 100 * 60 + close % 100;
        if (nowMinutes >= closeMinutes - closingBuffer) return ShopAvailability.ClosingSoon;
        return ShopAvailability.Open;
    }
}
