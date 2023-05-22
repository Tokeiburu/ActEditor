using System.IO;
using System.Media;
using System.Threading;

namespace ActEditor.Core {
	/// <summary>
	/// Handles the sound effect for the animation thread.
	/// </summary>
	public class SoundEffect {
		private bool _isStopped = true;
		private byte[] _soundFile;
		private Thread _soundThread;

		public bool IsFinished {
			get { return _isStopped; }
		}

		public void Play(byte[] file) {
			_soundFile = file;

			if (!_isStopped)
				return;

			_soundThread = new Thread(_playThread);
			_soundThread.Start();
		}

		private void _playThread() {
			if (_soundFile != null) {
				_isStopped = false;
				SoundPlayer player = new SoundPlayer();
				player.Stream = new MemoryStream(_soundFile);
				player.PlaySync();
				_isStopped = true;
			}
		}
	}
}