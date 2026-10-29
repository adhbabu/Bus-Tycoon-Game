using System;
using System.Collections.Generic;

namespace BusTycoon.Stations
{
    /// <summary>Oyuncunun bir şehirdeki durağı: yapılan geliştirmeler (kimlikleriyle) ve ilk geliştirmenin günü.</summary>
    [Serializable]
    public sealed class StationState
    {
        public string cityId;
        public int firstUpgradeDay;
        public List<string> upgrades = new List<string>();
    }
}
