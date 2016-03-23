using System;
using System.Configuration;
using System.IO;

namespace HolographicStudio.Utils
{
    public static class Configuration
    {
        private static string PublicDocuments = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);

        /// <summary>
        /// Location of the RoomAlive ensemble configuration
        /// </summary>
        public static string EnsembleConfigurationFile
        {
            get
            {
                var val = ConfigurationManager.AppSettings["EnsembleConfigurationFile"];
                if (!String.IsNullOrEmpty(val))
                {
                    return Path.Combine(PublicDocuments, val);
                }

                return Path.Combine(PublicDocuments, "HolographicStudio/calibration.xml");
            }
        }
    }
}