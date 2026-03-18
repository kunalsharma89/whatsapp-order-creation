using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProcessingLogConfiguration : IEntityTypeConfiguration<ProcessingLog>
{
    public void Configure(EntityTypeBuilder<ProcessingLog> builder)
    {
        builder.ToTable("ProcessingLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MessageId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(64);
    }
}
