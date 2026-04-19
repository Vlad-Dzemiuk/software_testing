using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Infrastructure.Persistence.Configurations;

public class SongConfiguration : IEntityTypeConfiguration<Song>
{
    public void Configure(EntityTypeBuilder<Song> builder)
    {
        builder.ToTable("Songs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Artist).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Album).IsRequired().HasMaxLength(300);
        builder.Property(e => e.DurationSeconds).IsRequired();
        builder.Property(e => e.Genre).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ReleaseDate).IsRequired();

        builder.HasIndex(e => e.Genre);
        builder.HasIndex(e => e.Artist);
    }
}
