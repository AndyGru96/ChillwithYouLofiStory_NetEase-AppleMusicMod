using UnityEngine.UI;

namespace MusicBridge;

internal interface IMusicModule
{
	MusicProvider Id { get; }

	bool HasTrack { get; }

	bool IsPlaying { get; }

	string Title { get; }

	string Artist { get; }

	double Position { get; }

	double Duration { get; }

	bool CanSeek { get; }

	bool SupportsLyrics { get; }

	bool Shuffle { get; set; }

	bool RepeatOne { get; set; }

	float Volume { get; }

	string IdleHint { get; }

	string StatusPrefix { get; }

	void ApplyCover(Image target);

	void TogglePlayPause();

	void Next();

	void Previous();

	void PauseIfPlaying();

	void SetVolume(float volume);

	void Seek(double seconds);
}
