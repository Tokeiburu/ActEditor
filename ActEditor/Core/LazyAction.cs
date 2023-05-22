using System;
using System.Collections.Generic;
using ErrorManager;
using GRF.Threading;

namespace ActEditor.Core {
	public class LazyAction {
		private static readonly Dictionary<int, object> _locks = new Dictionary<int, object>();
		private static readonly Dictionary<int, object> _locks2 = new Dictionary<int, object>();
		private static readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

		/// <summary>
		/// Executes an action and ignores any pending one.
		/// </summary>
		/// <param name="action">The action.</param>
		/// <param name="instance">The unique identifier for the action.</param>
		public static void Execute(Action action, int instance) {
			if (!_locks.ContainsKey(instance)) {
				_locks[instance] = new object();
				_locks2[instance] = new object();
				_counts[instance] = -1;
			}

			object oLock = _locks[instance];
			object qLock = _locks2[instance];

			lock (qLock) {
				_counts[instance]++;
			}

			GrfThread.Start(delegate {
				lock (oLock) {
					try {
						if (_counts[instance] > 0) {
							return;
						}

						action();
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
					finally {
						lock (qLock) {
							_counts[instance]--;
						}
					}
				}
			});
		}
	}
}
