using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalMessageId).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => x.ExternalMessageId).IsUnique();
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
