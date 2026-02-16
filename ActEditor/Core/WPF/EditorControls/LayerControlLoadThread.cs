using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		private Stopwatch _watch = new Stopwatch();
		private long _lastTick = 0;

		public LayerControlLoadThread(LayerEditor le) {
			_le = le;
			_watch.Start();
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

		private const long UpdateTick = 50;

		private void _start() {
			while (true) {
				if (!_isRunning)
					return;

				bool hasEntry = true;
				bool pendingUpdate = false;

				while (hasEntry && _isRunning) {
					long elapsed = _watch.ElapsedMilliseconds;

					// Don't make the thread sleep
					if (elapsed - _lastTick < UpdateTick) {
						Thread.Sleep((int)Math.Max(1, UpdateTick - (elapsed - _lastTick)));
					}

					_lastTick = _watch.ElapsedMilliseconds;

					HashSet<int> updateEntries;

					lock (_lock) {
						updateEntries = new HashSet<int>(_frames);
						_frames.Clear();
					}

					if (pendingUpdate) {
						//Console.WriteLine("Pending update, abort...");
						lock (_lock) {
							foreach (var layerIndex2 in updateEntries) {
								_frames.Add(layerIndex2);
							}
						}
					}
					else {
						pendingUpdate = true;

						_le.Dispatcher.BeginInvoke(new Action(() => {
							pendingUpdate = false;
							Stopwatch timer = Stopwatch.StartNew();

							foreach (var layerIndex in updateEntries) {
								// Possible that the layer doesn't exist anymore
								try {
									var l = _le.GetVisual(layerIndex);

									//if (layerIndex > 4)
									//	continue;
					
									if (l != null)
										l.InternalUpdate();

									if (timer.ElapsedMilliseconds > 10) {
										lock (_lock) {
											foreach (var layerIndex2 in updateEntries) {
												_frames.Add(layerIndex2);
											}
										}
										//Console.WriteLine("TOO LONG!!! abort");
										break;
									}
								}
								catch {
								}
							}

							//Console.WriteLine("Took: " + timer.ElapsedMilliseconds);
						}), DispatcherPriority.Background);
					}

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
		public void Update(HashSet<int> layerIndexes) {
			lock (_lock) {
				foreach (var layerIndex in layerIndexes)
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
