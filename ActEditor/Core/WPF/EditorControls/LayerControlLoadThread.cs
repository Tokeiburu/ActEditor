using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Delays the refresh rate of the layers by debouncing the calls.
	/// </summary>
	public class LayerControlLoadThread {
		private readonly LayerEditor _le;
		private readonly object _lock = new object();
		private readonly ManualResetEvent _threadHandle = new ManualResetEvent(false);
		private bool _threadIsEnabled = true;
		private bool _isRunning = true;
		public HashSet<int> _frames = new HashSet<int>();

		public LayerControlLoadThread(LayerEditor le) {
			_le = le;
		}

		public void Stop() {
			_isRunning = false;
			Enabled = true;
		}

		public bool Enabled {
			set {
				if (value) {
					if (!_threadIsEnabled)
						_threadHandle.Set();
				}
				else {
					if (_threadIsEnabled) {
						_threadIsEnabled = false;
						_threadHandle.Reset();
					}
				}
			}
		}

		public void Start() {
			new Thread(_start) { Name = "Act Editor - Layer editor thread" }.Start();
		}

		private void _start() {
			while (true) {
				if (!_isRunning)
					return;

				bool hasEntry = true;

				while (hasEntry && _isRunning) {
					Thread.Sleep(100);
					
					HashSet<int> updateEntries;

					lock (_lock) {
						updateEntries = new HashSet<int>(_frames);
						_frames.Clear();
					}

					_le.Dispatcher.BeginInvoke(new Action(() => {
						foreach (var layerIndex in updateEntries) {
							// Possible that the layer doesn't exist anymore
							try {
								_le.Get(layerIndex).InternalUpdate();
							}
							catch {
							}
						}
					}), DispatcherPriority.Background);

					if (!_isRunning)
						return;

					lock (_lock) {
						if (_frames.Count == 0) {
							hasEntry = false;
						}
					}
				}

				_threadIsEnabled = false;
				_threadHandle.Reset();
				_threadHandle.WaitOne();
			}
		}

		/// <summary>
		/// Updates the specified layer index, async.
		/// </summary>
		/// <param name="layerIndex">Index of the layer.</param>
		public void Update(int layerIndex) {
			lock (_lock) {
				_frames.Add(layerIndex);
			}

			Enabled = true;
		}

		/// <summary>
		/// Updates the specified frame indexes, async.
		/// </summary>
		/// <param name="layerIndexes">Indexes of the frame.</param>
		public void Update(int[] layerIndexes) {
			lock (_lock) {
				foreach (var layerIndex in layerIndexes)
					_frames.Add(layerIndex);
			}

			Enabled = true;
		}
	}
}
