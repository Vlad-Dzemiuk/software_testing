namespace MusicPlaylist.Domain.Entities;

public class PlaylistSong
{
    public long Id { get; set; }
    public long PlaylistId { get; set; }
    public long SongId { get; set; }
    public int Position { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    public Playlist Playlist { get; set; } = null!;
    public Song Song { get; set; } = null!;
}
