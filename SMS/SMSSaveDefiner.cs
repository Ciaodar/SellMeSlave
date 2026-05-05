using System.Collections.Generic;
using SMS.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.SaveSystem;

namespace SMS
{
    public class SMSSaveDefiner : SaveableTypeDefiner
    {
        // 987600 is chosen as a unique base ID for this mod to avoid conflicts.
        public SMSSaveDefiner() : base(987600)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(TownPrisonerStock), 1);
            AddClassDefinition(typeof(LordDeliveryData), 2);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<string, TroopRoster>));
            ConstructContainerDefinition(typeof(Dictionary<string, CampaignTime>));
            ConstructContainerDefinition(typeof(List<LordDeliveryData>));
        }
    }
}
