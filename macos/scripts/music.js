// macOS Music automation. JSON goes through stdin; never evaluate user/library text.
ObjC.import('Foundation');
function run() {
    var input = $.NSFileHandle.fileHandleWithStandardInput.readDataToEndOfFile;
    var req = JSON.parse(ObjC.unwrap($.NSString.alloc.initWithDataEncoding(input, $.NSUTF8StringEncoding)));
    var music = Application('com.apple.Music');
    if (!music.running()) {
        if (req.action === 'snapshot') return JSON.stringify({Valid: false});
        throw new Error('请先打开 macOS「音乐」App，再点连接。');
    }
    function playlist(id) {
        var p = music.userPlaylists.whose({persistentID: id})();
        if (!p.length) throw new Error('歌单已改变，请更新播放列表。');
        return p[0];
    }
    var result = {};
    switch (req.action) {
    case 'connect':
        result = {version: music.version(), volume: music.soundVolume() / 100};
        break;
    case 'playlists':
        result = music.userPlaylists().map(function(p, i) {
            var parent = null;
            try { parent = p.parent.persistentID(); } catch (_) {}
            return {Name: p.name(), PersistentId: p.persistentID(), ParentId: parent,
                IsFolder: p.class() === 'folder playlist', Order: i};
        });
        break;
    case 'tracks':
        var p = playlist(req.playlistId);
        // Bulk properties avoid one Apple Event per field per song.
        var ids = p.tracks.persistentID(), names = p.tracks.name(), artists = p.tracks.artist();
        var albums = p.tracks.album(), durations = p.tracks.duration();
        if ([names, artists, albums, durations].some(function(a) { return a.length !== ids.length; }))
            throw new Error('扫描期间歌单已改变，请重试。');
        result = ids.map(function(id, i) {
            var seconds = Math.max(0, Math.round(durations[i] || 0));
            return {PersistentId: id, Name: names[i], Artists: artists[i], Album: albums[i],
                DurationText: Math.floor(seconds / 60) + ':' + ('0' + seconds % 60).slice(-2), RowIndex: i};
        });
        break;
    case 'snapshot':
        var state = music.playerState();
        result = {Valid: false};
        try {
            var t = music.currentTrack;
            result = {Valid: true, AppId: 'AppleMusic.macOS', Title: t.name(), Artist: t.artist(),
                AlbumTitle: t.album(), AlbumArtist: t.albumArtist(),
                Status: state === 'playing' ? 4 : state === 'paused' ? 5 : 3,
                PositionSeconds: music.playerPosition(), DurationSeconds: t.duration(),
                CanPause: true, CanNext: true, CanPrev: true, CanSeek: true};
        } catch (_) {}
        break;
    case 'play':
        var tracks = playlist(req.playlistId).tracks.whose({persistentID: req.trackId})();
        if (!tracks.length) throw new Error('歌曲已不在歌单中，请更新播放列表。');
        music.play(tracks[0], {once: true});
        break;
    case 'pause': music.pause(); break;
    case 'toggle': music.playpause(); break;
    case 'next': music.nextTrack(); break;
    case 'previous': music.previousTrack(); break;
    case 'seek':
        if (typeof req.seconds !== 'number' || !isFinite(req.seconds) || req.seconds < 0) throw new Error('无效播放位置');
        music.playerPosition = req.seconds; break;
    case 'volume': result = {volume: music.soundVolume() / 100}; break;
    case 'setVolume':
        if (typeof req.volume !== 'number' || !isFinite(req.volume)) throw new Error('无效音量');
        music.soundVolume = Math.round(Math.max(0, Math.min(1, req.volume)) * 100); break;
    default: throw new Error('Unknown MusicBridge action');
    }
    return JSON.stringify(result);
}
