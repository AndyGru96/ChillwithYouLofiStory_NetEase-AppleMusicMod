using System.Net;

namespace MusicBridge;

internal sealed class NeteaseRequestCancellation
{
	private readonly object _gate = new object();

	private HttpWebRequest _request;

	public volatile bool IsCancelled;

	public void Attach(HttpWebRequest request)
	{
		lock (_gate)
		{
			_request = request;
			if (IsCancelled && _request != null)
			{
				_request.Abort();
			}
		}
	}

	public void Detach(HttpWebRequest request)
	{
		lock (_gate)
		{
			if (_request == request)
			{
				_request = null;
			}
		}
	}

	public void Cancel()
	{
		IsCancelled = true;
		lock (_gate)
		{
			if (_request != null)
			{
				try
				{
					_request.Abort();
					return;
				}
				catch
				{
					return;
				}
			}
		}
	}
}
