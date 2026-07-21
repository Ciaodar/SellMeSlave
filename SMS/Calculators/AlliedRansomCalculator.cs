using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SMS.Calculators
{
    public static class AlliedRansomCalculator
    {
        /// <summary>
        /// Calculates the probability of a ransom offer being accepted.
        /// Minimum probability is 30%, Maximum is 95%.
        /// Scales with the ratio of the offer amount to the normal ransom value.
        /// </summary>
        public static float CalculateAcceptanceProbability(int offerAmount, int normalRansomValue)
        {
            if (normalRansomValue <= 0) return 0.95f;

            float ratio = (float)offerAmount / normalRansomValue;
            
            // At ratio 1.0 (exact normal ransom), probability is 30%.
            // At ratio 2.0 (double normal ransom), probability is 95%.
            float probability = 0.3f + (ratio - 1f) * 0.65f;

            return MathF.Clamp(probability, 0.3f, 0.95f);
        }
    }
}
