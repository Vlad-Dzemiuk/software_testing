using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicPlaylist.Domain.Entities;

namespace MusicPlaylist.Infrastructure.Persistence.Configurations;

public class PlaylistSongConfiguration : IEntityTypeConfiguration<PlaylistSong>
{
    public void Configure(EntityTypeBuilder<PlaylistSong> builder)
    {
        builder.ToTable("PlaylistSongs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlaylistId).IsRequired();
        builder.Property(e => e.SongId).IsRequired();
        builder.Property(e => e.Position).IsRequired();
        builder.Property(e => e.AddedAt).IsRequired();

        builder
            .HasOne(e => e.Playlist)
            .WithMany(p => p.PlaylistSongs)
            .HasForeignKey(e => e.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(e => e.Song)
            .WithMany(s => s.PlaylistSongs)
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PlaylistId, e.SongId }).IsUnique();
        builder.HasIndex(e => new { e.PlaylistId, e.Position }).IsUnique();
    }
}
