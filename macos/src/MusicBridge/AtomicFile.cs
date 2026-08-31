using System;
using System.IO;
using System.Text;

namespace MusicBridge;

internal static class AtomicFile
{
	public static void WriteAllText(string path, string content)
	{
		WriteAllBytes(path, Encoding.UTF8.GetBytes(content ?? ""));
	}

	public static void WriteAllBytes(string path, byte[] content)
	{
		if (path == null)
		{
			throw new ArgumentNullException("path");
		}
		if (content == null)
		{
			throw new ArgumentNullException("content");
		}
		string text = BridgePaths.ValidateWritePath(path);
		string directoryName = Path.GetDirectoryName(text);
		if (string.IsNullOrEmpty(directoryName))
		{
			throw new IOException("目标文件没有父目录。");
		}
		Directory.CreateDirectory(directoryName);
		string text2 = text + ".tmp." + Guid.NewGuid().ToString("N");
		try
		{
			using (FileStream fileStream = new FileStream(text2, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				fileStream.Write(content, 0, content.Length);
				fileStream.Flush(flushToDisk: true);
			}
			Replace(text2, text);
			text2 = null;
		}
		finally
		{
			if (!string.IsNullOrEmpty(text2))
			{
				try
				{
					if (File.Exists(text2))
					{
						File.Delete(text2);
					}
				}
				catch
				{
				}
			}
		}
	}

	public static void Replace(string source, string destination)
	{
		string text = BridgePaths.ValidateWritePath(source);
		string text2 = BridgePaths.ValidateWritePath(destination);
		if (!string.Equals(Path.GetDirectoryName(text), Path.GetDirectoryName(text2), StringComparison.OrdinalIgnoreCase))
		{
			throw new IOException("原子替换要求源文件和目标文件位于同一目录。");
		}
		if (File.Exists(text2))
		{
			File.Replace(text, text2, null, ignoreMetadataErrors: true);
		}
		else
		{
			File.Move(text, text2);
		}
	}
}
