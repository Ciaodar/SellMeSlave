namespace SMS.Config
{
    public class SmsJsonModel
    {
        public float SlavePriceMultiplier { get; set; } = 4.0f;
        public bool EnableRandomization { get; set; } = true;
        public float LordPriceMultiplier { get; set; } = 4.0f;
        public int MaxSlavesPerBroker { get; set; } = 15;

        // Buy Slave Menu
        public bool EnableBuySlaveMenu { get; set; } = true;
        public int MinSlavesPerBroker { get; set; } = 5;
        public int StockExpirationDays { get; set; } = 3;

        // Lord Purchase
        public int MaxLordTransferCount { get; set; } = 2;
        public float LordEscapeChancePerDay { get; set; } = 0.05f;
        public float LordDeliverySpeedMultiplier { get; set; } = 1.0f;
        public bool EnablePrisonerExchange { get; set; } = true;
        public float CrimeRatingMultiplier { get; set; } = 1.0f;
        public float RelationGainMultiplier { get; set; } = 1.0f;
        public float RogueryXpMultiplier { get; set; } = 1.0f;
        public float HonorLossMultiplier { get; set; } = 1.0f;
    }
}
