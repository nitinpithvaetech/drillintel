using System;

namespace DrillIntel.Data.Objects.DataObjects.Models.AutoTagDetection
{
    /// <summary>
    /// Represents an activity-to-phase/tag mapping for automated drilling phase tag detection.
    /// Maps activity codes (e.g., RigUp, Drilling, CUC, TripOut, TripIn, Casing)
    /// to their corresponding PhaseId, StepId, and EmphId.
    /// </summary>
    public class TagMapping
    {
        public string ActivityType { get; set; } = string.Empty;
        public string PhaseId { get; set; } = string.Empty;
        public string StepId { get; set; } = string.Empty;
        public string EmphId { get; set; } = string.Empty;

        public TagMapping()
        {
        }

        public TagMapping(string activityType, string phaseId, string stepId, string emphId = "")
        {
            ActivityType = activityType ?? string.Empty;
            PhaseId = phaseId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            EmphId = emphId ?? string.Empty;
        }

        public TagMapping GetCopy()
        {
            return new TagMapping
            {
                ActivityType = this.ActivityType,
                PhaseId = this.PhaseId,
                StepId = this.StepId,
                EmphId = this.EmphId
            };
        }

        public override string ToString()
        {
            return $"{ActivityType} -> Phase: {PhaseId}, Step: {StepId}, Emph: {EmphId}";
        }
    }
}

