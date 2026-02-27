using ECommons;
using ECommons.DalamudServices;
using ECommons.Reflection;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace Artisan.RawInformation
{
    internal static class DalamudInfo
    {
        public static bool StagingChecked = false;
        public static bool IsStaging = false;
        public static bool IsOnStaging()
        {
            if (StagingChecked)
            {
                return IsStaging;
            }

            try
            {
                var v = Svc.PluginInterface.GetDalamudVersion();
                if (string.IsNullOrWhiteSpace(v?.BetaTrack))
                {
                    StagingChecked = true;
                    IsStaging = false;
                    return false;
                }

                if (string.Equals(v.BetaTrack, "release", StringComparison.CurrentCultureIgnoreCase))
                {
                    StagingChecked = true;
                    IsStaging = false;
                    return false;
                }
                else
                {
                    StagingChecked = true;
                    IsStaging = true;
                    return true;
                }
            }
            catch(Exception ex)
            {
                ex.Log("Probably CN or something");
                StagingChecked = true;
                IsStaging = false;
                return false;
            }
        }
    }
}
