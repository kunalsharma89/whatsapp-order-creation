using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class FailedMessageConfiguration : IEntityTypeConfiguration<FailedMessage>
{
    public void Configure(EntityTypeBuilder<FailedMessage> builder)
    {
        builder.ToTable("FailedMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MessageId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.Error).IsRequired();
        builder.Property(x => x.SourceQueue).IsRequired().HasMaxLength(128);
    }
}
