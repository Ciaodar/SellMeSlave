using System;

namespace SMS.Interop
{
    public class SmsSlavePurchaseRecord
    {
        public string BuyerHeroId { get; set; } = string.Empty;
        public bool IsLordPurchase { get; set; }
        public string? PurchasedLordId { get; set; }
        public int PurchasedTroopCount { get; set; }
        public int GoldPaid { get; set; }
        public string? SettlementId { get; set; }
        public float CampaignTimeDays { get; set; }
    }

    public static class SmsInteropEvents
    {
        public static event Action<SmsSlavePurchaseRecord>? SlavePurchased;

        public static void RaiseSlavePurchased(SmsSlavePurchaseRecord record)
        {
            SlavePurchased?.Invoke(record);
        }
    }
}
