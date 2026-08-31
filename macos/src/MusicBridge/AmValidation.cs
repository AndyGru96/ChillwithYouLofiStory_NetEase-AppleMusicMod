using System.Collections.Generic;

namespace MusicBridge;

internal sealed class AmValidation
{
	public bool Ok;

	public readonly List<string> Problems = new List<string>();

	public int NodeCount;

	public int TrackCount;

	public int FailedPlaylists;

	public bool HasStructuralProblem;

	public string Fingerprint;
}
