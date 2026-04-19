using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Infrastructure.Persistence.Configurations;

public class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder.ToTable("Playlists");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(128);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.IsPublic).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
    }
}
