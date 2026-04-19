SELECT
  (SELECT COUNT(*) FROM "Songs") AS songs,
  (SELECT COUNT(*) FROM "Playlists") AS playlists,
  (SELECT COUNT(*) FROM "PlaylistSongs") AS playlist_songs,
  (SELECT COUNT(*) FROM "Songs") + (SELECT COUNT(*) FROM "Playlists") + (SELECT COUNT(*) FROM "PlaylistSongs") AS total;
