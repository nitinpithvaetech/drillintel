using System;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    /// <summary>
    /// Type bridge maintaining backward compatibility with callers referencing
    /// SystemSettings under the DrillIntel.Data.Objects.DataObjects.Models namespace.
    /// Inherits from DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings.
    /// </summary>
    public class SystemSettings : Common.SystemSettings
    {
        public new SystemSettings GetCopy()
        {
            var copy = new SystemSettings
            {
                LastError = this.LastError
            };

            if (this.settings != null)
            {
                foreach (var kvp in this.settings)
                {
                    copy.settings[kvp.Key] = new Common.SettingValue
                    {
                        SettingID = kvp.Value.SettingID,
                        SettingName = kvp.Value.SettingName,
                        Value = kvp.Value.Value,
                        SettingType = kvp.Value.SettingType,
                        NeedToSave = kvp.Value.NeedToSave
                    };
                }
            }

            return copy;
        }
    }
}

