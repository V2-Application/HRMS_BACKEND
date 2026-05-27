// Partial extension of HRMSContext adding the DbSet + model config for
// tblEmployee_MedicalCard. Kept in its own file so the auto-generated
// HRMSContext.cs stays clean.
#nullable disable
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Data;

public partial class HRMSContext
{
    public virtual DbSet<tblEmployee_MedicalCard> tblEmployee_MedicalCards { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<tblEmployee_MedicalCard>(entity =>
        {
            entity.ToTable("tblEmployee_MedicalCard");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Ecode, "IX_tblEmployee_MedicalCard_Ecode");
            entity.HasIndex(e => e.EmployeeId, "IX_tblEmployee_MedicalCard_EmployeeId");
            entity.HasIndex(e => new { e.EmployeeId, e.CardOrder }, "UK_tblEmployee_MedicalCard_Emp_Order").IsUnique();

            entity.Property(e => e.Ecode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UhidNo).HasMaxLength(50);
            entity.Property(e => e.HolderName).HasMaxLength(200);
            entity.Property(e => e.Gender).HasMaxLength(1).IsFixedLength();
            entity.Property(e => e.PolicyNo).HasMaxLength(100);
            entity.Property(e => e.Organisation).HasMaxLength(200);
            entity.Property(e => e.Insurer).HasMaxLength(200);
            entity.Property(e => e.Tpa).HasMaxLength(200);
            entity.Property(e => e.SumAssured).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SourcePdfUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedOn).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}
