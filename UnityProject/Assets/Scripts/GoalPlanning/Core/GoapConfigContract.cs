using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
    public static class GoapConfigContract
    {
        public static readonly HashSet<string> ApprovedTopLevelKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "maxPlanDepth",
            "replanCooldownSec",
            "goalSwitchHysteresis",
            "plannerBudgetPerTick",
            "emergencyOverrideCoreHpPct",
            "actionFailureRetryLimit",
            "planStaleTimeoutSec",
            "homeBiasWeight",
            "awaySafetyWeight"
        };

        public static bool ValidateTopLevelKeys(IEnumerable<string> keys, out string error)
        {
            error = null;

            if (keys == null)
            {
                error = "GOAP config keys are null.";
                return false;
            }

            HashSet<string> set = new HashSet<string>(keys, StringComparer.Ordinal);

            foreach (string approved in ApprovedTopLevelKeys)
            {
                if (!set.Contains(approved))
                {
                    error = $"Missing required GOAP config key: {approved}";
                    return false;
                }
            }

            foreach (string key in set)
            {
                if (!ApprovedTopLevelKeys.Contains(key))
                {
                    error = $"Out-of-scope GOAP config key: {key}";
                    return false;
                }
            }

            return true;
        }
    }

}
