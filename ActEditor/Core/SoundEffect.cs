using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;

namespace ActEditor.Core {
	/// <summary>
	/// Handles the sound effect for the animation thread.
	/// </summary>
	public class SoundEffect {
		private int _isPlaying = 0;

		public void Play(byte[] soundFileData) {
			if (Interlocked.Exchange(ref _isPlaying, 1) == 1)
				return;

			Task.Run(() => {
				try {
					using (var stream = new MemoryStream(soundFileData))
					using (var player = new SoundPlayer(stream)) {
						player.PlaySync();
					}
				}
				finally {
					Interlocked.Exchange(ref _isPlaying, 0);
				}
			});
		}
	}
}