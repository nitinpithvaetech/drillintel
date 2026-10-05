using System;
using System.IO;

namespace DrillIntel.Data
{
    /// <summary>
    /// Centralized provider for accessing the Base Application Database (DrillIntelApp.sqlite).
    /// Enforces the Single Source of Truth (SSOT) principle for application-level data such as
    /// Unit Master (APP_UNIT_MASTER) and Unit Conversions (APP_UNIT_CONVERSIONS).
    /// </summary>
    public static class BaseDatabaseProvider
    {
        private static readonly object _lock = new();
        private static IDataServiceDIntel? _baseDataService;

        /// <summary>
        /// Gets or sets the primary Base Database data service instance.
        /// </summary>
        public static IDataServiceDIntel? BaseDataService
        {
            get => _baseDataService;
            set
            {
                lock (_lock)
                {
                    _baseDataService = value;
                }
            }
        }

        /// <summary>
        /// Returns an active connection to the Base Database.
        /// If not already initialized, attempts discovery in common system paths.
        /// </summary>
        public static IDataServiceDIntel? GetBaseDataService()
        {
            lock (_lock)
            {
                if (_baseDataService != null && _baseDataService.IsConnectionOpen())
                {
                    return _baseDataService;
                }

                // 1. ProgramData location
                try
                {
                    string programDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "DrillIntel",
                        "DrillIntelApp.sqlite");

                    if (File.Exists(programDataPath))
                    {
                        var ds = new DataServiceDIntel(programDataPath);
                        if (ds.IsConnectionOpen())
                        {
                            _baseDataService = ds;
                            return _baseDataService;
                        }
                    }
                }
                catch { }

                // 2. Base directory Data folder location
                try
                {
                    string baseDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "DrillIntelApp.sqlite");
                    if (File.Exists(baseDataPath))
                    {
                        var ds = new DataServiceDIntel(baseDataPath);
                        if (ds.IsConnectionOpen())
                        {
                            _baseDataService = ds;
                            return _baseDataService;
                        }
                    }
                }
                catch { }

                return _baseDataService;
            }
        }
    }
}

