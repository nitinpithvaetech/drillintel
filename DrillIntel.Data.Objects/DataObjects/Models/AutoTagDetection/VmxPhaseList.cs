using System;

namespace DrillIntel.Data.Objects.DataObjects.Models.AutoTagDetection
{
    /// <summary>
    /// Represents an entity record in the VMX_PHASE_LIST SQLite table,
    /// corresponding to an operational drilling phase tag.
    /// </summary>
    public class VmxPhaseList
    {
        public double PhaseIndex { get; set; }
        public string WellId { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string PhaseId { get; set; } = string.Empty;
        public string StepId { get; set; } = string.Empty;
        public string EmphId { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public string? ModifiedDate { get; set; }
        public int IsOpen { get; set; } = 0;

        public VmxPhaseList()
        {
        }

        public VmxPhaseList(
            double phaseIndex,
            string wellId,
            string startDate,
            string endDate,
            string phaseId,
            string stepId,
            string emphId,
            string? notes = null,
            string? createdBy = null,
            string? createdDate = null,
            string? modifiedBy = null,
            string? modifiedDate = null,
            int isOpen = 0)
        {
            PhaseIndex = phaseIndex;
            WellId = wellId ?? string.Empty;
            StartDate = startDate ?? string.Empty;
            EndDate = endDate ?? string.Empty;
            PhaseId = phaseId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            EmphId = emphId ?? string.Empty;
            Notes = notes;
            CreatedBy = createdBy;
            CreatedDate = createdDate;
            ModifiedBy = modifiedBy;
            ModifiedDate = modifiedDate;
            IsOpen = isOpen;
        }

        public VmxPhaseList GetCopy()
        {
            return new VmxPhaseList
            {
                PhaseIndex = this.PhaseIndex,
                WellId = this.WellId,
                StartDate = this.StartDate,
                EndDate = this.EndDate,
                PhaseId = this.PhaseId,
                StepId = this.StepId,
                EmphId = this.EmphId,
                Notes = this.Notes,
                CreatedBy = this.CreatedBy,
                CreatedDate = this.CreatedDate,
                ModifiedBy = this.ModifiedBy,
                ModifiedDate = this.ModifiedDate,
                IsOpen = this.IsOpen
            };
        }
    }
}

